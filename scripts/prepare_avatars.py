# Copyright (c) 2026 Navin Ratnayake (@navinrtnk). SPDX-License-Identifier: MIT
"""Build standing, skinned glTF patrons from verified CC0 MakeHuman graphics.

Development dependency: NumPy. Run fetch_customer.py first. No modeling application
code is used. Garments, skin, hair and eyes share one rest pose and real skin weights.
"""
from collections import defaultdict
from filecmp import cmp
from pathlib import Path
import json
import math
import shutil
import struct
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / ".tools/customer-source"
OUTPUT = ROOT / "Assets/Customer/Avatars"
OFFSET = np.array([0, .89, 0])


def read_obj(path):
    vertices, uvs, faces = [], [], []
    group = ""
    for line in path.read_text().splitlines():
        words = line.split()
        if not words:
            continue
        if words[0] == "v":
            vertices.append(tuple(map(float, words[1:4])))
        elif words[0] == "vt":
            uvs.append(tuple(map(float, words[1:3])))
        elif words[0] == "g":
            group = " ".join(words[1:])
        elif words[0] == "f":
            face = [tuple(int(x) - 1 for x in word.split("/")[:2]) for word in words[1:]]
            faces.append((face, group))
    return vertices, uvs, faces


def unit(v):
    return v / max(1e-10, np.linalg.norm(v))


def align(a, b):
    a, b = unit(a), unit(b)
    v, c = np.cross(a, b), np.dot(a, b)
    skew = np.array([[0,-v[2],v[1]], [v[2],0,-v[0]], [-v[1],v[0],0]])
    return np.eye(3) + skew + skew @ skew / max(1e-8, 1+c)


def frame(forward, across):
    f = unit(forward)
    x = unit(across-f*np.dot(across,f))
    return np.column_stack([x, f, np.cross(f,x)])


def subdivide(attributes, uvs, faces):
    """Catmull-Clark geometry and skin-weight interpolation, with separate UV seams."""
    face_values = np.array([attributes[[v for v,_ in f]].mean(0) for f,_ in faces])
    edges, adjacent, neighbors, boundary = defaultdict(list), defaultdict(list), defaultdict(set), defaultdict(list)
    for fi,(face,_) in enumerate(faces):
        for j,(a,_) in enumerate(face):
            b=face[(j+1)%len(face)][0]
            edges[tuple(sorted((a,b)))].append(fi)
            adjacent[a].append(fi)
    for (a,b), attached in edges.items():
        neighbors[a].add(b); neighbors[b].add(a)
        if len(attached)==1: boundary[a].append(b); boundary[b].append(a)
    points=attributes.copy()
    for i, attached in adjacent.items():
        p=attributes[i]
        if len(boundary[i])==2:
            points[i]=(p*6+attributes[boundary[i]].sum(0))/8
        elif len(attached)>=3:
            f=face_values[attached].mean(0)
            r=(p+attributes[list(neighbors[i])].mean(0))*.5
            points[i]=(f+2*r+(len(attached)-3)*p)/len(attached)
    edge_values=[]; edge_ids={}
    for (a,b),attached in edges.items():
        edge_ids[(a,b)]=len(points)+len(edge_values)
        edge_values.append((attributes[a]+attributes[b]+face_values[attached].sum(0))/4
                           if len(attached)==2 else (attributes[a]+attributes[b])*.5)
    center_start=len(points)+len(edge_values)
    points=np.vstack([points,edge_values,face_values])
    texcoords=list(uvs); result=[]
    for fi,(face,group) in enumerate(faces):
        mid=len(texcoords);texcoords.append(np.mean([uvs[t] for _,t in face],0))
        for j,(v,t) in enumerate(face):
            before,after=face[(j-1)%len(face)],face[(j+1)%len(face)]
            ta=len(texcoords);texcoords.append((np.array(uvs[t])+uvs[after[1]])*.5)
            tb=len(texcoords);texcoords.append((np.array(uvs[t])+uvs[before[1]])*.5)
            result.append(([(v,t),(edge_ids[tuple(sorted((v,after[0])))],ta),
                (center_start+fi,mid),(edge_ids[tuple(sorted((before[0],v)))],tb)],group))
    return points,np.array(texcoords),result


class Avatar:
    def __init__(self, target=None, female=False):
        raw,self.uvs,self.faces=read_obj(SOURCE/"base.obj")
        raw=np.array(raw)
        if target:
            for line in (SOURCE/(target+".target")).read_text().splitlines():
                w=line.split()
                if w and not w[0].startswith("#"): raw[int(w[0])]+=np.array(w[1:4],float)
        self.base=raw*.1
        self.rig=json.loads((SOURCE/"default.mhskel").read_text())
        bones=self.rig["bones"]
        self.names=[]
        def visit(name):
            if name in self.names:return
            if bones[name]["parent"]:visit(bones[name]["parent"])
            self.names.append(name)
        for name in bones:visit(name)
        self.ids={name:i for i,name in enumerate(self.names)}
        self.weights=np.zeros((len(self.base),len(self.names)))
        for name,entries in json.loads((SOURCE/"default_weights.mhw").read_text())["weights"].items():
            for vertex,weight in entries:self.weights[vertex,self.ids[name]]=weight
        self.transforms={}
        def set_transform(names, origin, target, rotation):
            for name in names:self.transforms[name]=(rotation,target-rotation@origin)
        for side,sign in [("L",1),("R",-1)]:
            a,b,c=[self.head(n+"."+side) for n in ["upperarm01","lowerarm01","wrist"]]
            elbow=np.array([sign*.23,1.17-OFFSET[1],.02])
            elbow=a+unit(elbow-a)*np.linalg.norm(b-a)
            wrist=np.array([sign*.24,.94-OFFSET[1],.08])
            wrist=elbow+unit(wrist-elbow)*np.linalg.norm(c-b)
            set_transform(["upperarm01."+side,"upperarm02."+side],a,a,align(b-a,elbow-a))
            set_transform(["lowerarm01."+side,"lowerarm02."+side],b,elbow,align(c-b,wrist-elbow))
            forward=self.tail("finger3-3."+side)-c
            across=self.tail("finger2-3."+side)-self.tail("finger5-3."+side)
            hand=frame(np.array([0,-1,.08]),np.array([-sign,0,0]))@frame(forward,across).T
            set_transform([n for n in self.names if n.endswith("."+side) and
                n.startswith(("wrist","metacarpal","finger"))],c,wrist,hand)
        self.joints=np.array([self.transform(self.head(n),n)+OFFSET for n in self.names])
        skin=[]
        for f,g in self.faces:
            if g!="body":continue
            p=self.base[[i for i,_ in f]]
            head=np.all(p[:,1]>.445) and np.all(np.abs(p[:,0])<.19)
            hands=np.all(np.abs(p[:,0])>.391) and np.all(p[:,1]>.05)
            # Extend covered leg geometry beneath the skirt hem. Cropping at the
            # knee leaves an open gap after the seated pose and subdivision.
            calves=female and np.all(p[:,1]<-.20)
            if head or hands or calves:skin.append((f,g))
        self.skin_faces=skin

    def joint(self,name):return self.base[self.rig["joints"][name]].mean(0)
    def head(self,name):return self.joint(self.rig["bones"][name]["head"])
    def tail(self,name):return self.joint(self.rig["bones"][name]["tail"])
    def transform(self,p,name):
        r,t=self.transforms.get(name,(np.eye(3),np.zeros(3)))
        return r@p+t

    def pose(self,vertices,weights):
        weights=weights.copy();total=weights.sum(1)
        weights/=np.maximum(total[:,None],1e-10)
        result=np.zeros_like(vertices)
        for name,i in self.ids.items():
            r,t=self.transforms.get(name,(np.eye(3),np.zeros(3)))
            result+=(vertices@r.T+t)*weights[:,i,None]
        result[total<1e-8]=vertices[total<1e-8]
        return result+OFFSET,weights

    def fitted(self,stem):
        vertices,uvs,faces=read_obj(SOURCE/(stem+".obj"))
        maps=[];scales=np.ones(3);section=""
        for line in (SOURCE/(stem+".mhclo")).read_text().splitlines():
            w=line.split()
            if not w or w[0].startswith("#"):continue
            if w[0] in ["x_scale","y_scale","z_scale"]:
                axis="xyz".index(w[0][0]);scales[axis]=abs(self.base[int(w[1]),axis]-self.base[int(w[2]),axis])/(float(w[3])*.1)
            elif w[0]=="verts":section="verts"
            elif w[0]=="delete_verts":section="delete"
            elif section=="verts" and w[0].isdigit():
                maps.append(([int(w[0])],[1.],np.zeros(3)) if len(w)==1 else
                    ([int(i) for i in w[:3]],list(map(float,w[3:6])),np.array(w[6:9],float)*.1))
        assert len(maps)==len(vertices),(stem,len(maps),len(vertices))
        vertices=np.array([self.base[ids].T@amount+offset*scales for ids,amount,offset in maps])
        weights=np.array([self.weights[ids].T@amount for ids,amount,_ in maps])
        if stem=="high-poly":faces=[(f,g) for f,g in faces if not all(uvs[t][0]>.85 and uvs[t][1]<.15 for _,t in f)]
        return vertices,weights,np.array(uvs),faces


class GLB:
    def __init__(self,avatar):
        self.avatar=avatar;self.data=bytearray()
        self.doc={"asset":{"version":"2.0","generator":"Booming Store avatar pipeline, adapted from Bartender Specialogist; CC0 graphics"},
            "scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"name":"Avatar","children":[]}],
            "buffers":[{}],"bufferViews":[],"accessors":[],"meshes":[],"materials":[],"textures":[],"images":[],"skins":[]}
        for i,name in enumerate(avatar.names):
            parent=avatar.rig["bones"][name]["parent"]
            position=avatar.joints[i]-(avatar.joints[avatar.ids[parent]] if parent else 0)
            self.doc["nodes"].append({"name":name.replace(".","_"),"translation":position.tolist(),"children":[]})
            self.doc["nodes"][avatar.ids[parent]+1 if parent else 0]["children"].append(i+1)
        matrices=np.repeat(np.eye(4)[None],len(avatar.names),axis=0)
        matrices[:,:3,3]=-avatar.joints
        accessor=self.array(matrices.transpose(0,2,1).reshape(-1,16),"MAT4")
        self.doc["skins"].append({"name":"PatronRig","joints":list(range(1,len(avatar.names)+1)),"skeleton":avatar.ids["root"]+1,"inverseBindMatrices":accessor})

    def array(self,values,kind,component=5126,target=None,bounds=False):
        dtype={5126:"<f4",5125:"<u4",5123:"<u2"}[component]
        values=np.asarray(values,dtype=dtype)
        while len(self.data)%4:self.data.append(0)
        view={"buffer":0,"byteOffset":len(self.data),"byteLength":values.nbytes}
        if target:view["target"]=target
        self.doc["bufferViews"].append(view);self.data+=values.tobytes()
        accessor={"bufferView":len(self.doc["bufferViews"])-1,"componentType":component,"count":len(values),"type":kind}
        if bounds:accessor.update(min=values.min(0).tolist(),max=values.max(0).tolist())
        self.doc["accessors"].append(accessor)
        return len(self.doc["accessors"])-1

    def texture(self,path):
        self.doc["images"].append({"uri":"../"+path})
        self.doc["textures"].append({"source":len(self.doc["images"])-1})
        return {"index":len(self.doc["textures"])-1}

    def material(self,name,albedo,normal=None,ao=None,alpha=False,color=None):
        pbr={"baseColorTexture":self.texture(albedo),"metallicFactor":0,"roughnessFactor":.6 if name=="Skin" else .85}
        if color:pbr["baseColorFactor"]=color
        material={"name":name,"pbrMetallicRoughness":pbr,"doubleSided":alpha or name=="Clothing"}
        if normal:material["normalTexture"]=dict(self.texture(normal),scale=.7)
        if ao:material["occlusionTexture"]=dict(self.texture(ao),strength=.7)
        if alpha:material.update(alphaMode="MASK",alphaCutoff=.4)
        self.doc["materials"].append(material)
        return len(self.doc["materials"])-1

    def mesh(self,name,vertices,weights,uvs,faces,material,smooth=False,eye=False):
        vertices,weights=self.avatar.pose(vertices,weights)
        if smooth:
            values,uvs,faces=subdivide(np.hstack([vertices,weights]),uvs,faces)
            vertices,weights=values[:,:3],values[:,3:]
        triangles=[]
        for f,_ in faces:
            for j in range(1,len(f)-1):triangles.append([f[0],f[j],f[j+1]])
        if not triangles:return
        norms=np.zeros_like(vertices)
        for triangle in triangles:
            ids=[p[0] for p in triangle]
            a,b,c=vertices[ids]
            n=np.cross(b-a,c-a)
            for i in ids:norms[i]+=n
        norms/=np.maximum(np.linalg.norm(norms,axis=1,keepdims=True),1e-10)
        mapping={};positions=[];normals=[];coords=[];influences=[];indices=[]
        for triangle in triangles:
            for key in triangle:
                if key not in mapping:
                    i,t=key;mapping[key]=len(positions)
                    positions.append(vertices[i]);normals.append(norms[i]);coords.append([uvs[t][0],1-uvs[t][1]])
                    w=weights[i].copy()
                    if eye:
                        w[:]=0;w[self.avatar.ids['eye.L' if vertices[i][0]>0 else 'eye.R']]=1
                    influences.append(w)
                indices.append(mapping[key])
        weights=np.maximum(np.array(influences),0)
        joints=np.argsort(weights,axis=1)[:,-8:][:,::-1]
        weights=np.take_along_axis(weights,joints,axis=1)
        weights/=np.maximum(weights.sum(1,keepdims=True),1e-10)
        attributes={"POSITION":self.array(positions,"VEC3",target=34962,bounds=True),
            "NORMAL":self.array(normals,"VEC3",target=34962),"TEXCOORD_0":self.array(coords,"VEC2",target=34962)}
        for layer in range(2):
            attributes[f"JOINTS_{layer}"]=self.array(joints[:,layer*4:layer*4+4],"VEC4",5123,34962)
            attributes[f"WEIGHTS_{layer}"]=self.array(weights[:,layer*4:layer*4+4],"VEC4",target=34962)
        self.doc["meshes"].append({"name":name,"primitives":[{"attributes":attributes,"indices":self.array(np.array(indices).reshape(-1),"SCALAR",5125,34963),"material":material}]})
        self.doc["nodes"][0]["children"].append(len(self.doc["nodes"]))
        self.doc["nodes"].append({"name":name,"mesh":len(self.doc["meshes"])-1,"skin":0})

    def save(self,path):
        self.doc["buffers"][0]["byteLength"]=len(self.data)
        text=json.dumps(self.doc,separators=(",",":")).encode()
        text+=b" "*((-len(text))%4);self.data+=b"\0"*((-len(self.data))%4)
        with path.open("wb") as f:
            f.write(struct.pack("<4sII",b"glTF",2,12+8+len(text)+8+len(self.data)))
            f.write(struct.pack("<I4s",len(text),b"JSON"));f.write(text)
            f.write(struct.pack("<I4s",len(self.data),b"BIN\0"));f.write(self.data)
        print(path.name,len(self.data),"bytes",flush=True)


def prepare():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    profiles=[
        ("gentleman_a","caucasian-male-young","male_casualsuit01","short02","middleage_lightskinned_male_diffuse.png"),
        ("gentleman_b","african-male-young","male_casualsuit01","short04","young_darkskinned_male_diffuse.png"),
        ("woman_a","caucasian-female-young","female_elegantsuit01","long01","young_lightskinned_female_diffuse.png"),
        ("woman_b","asian-female-young","female_elegantsuit01","bob01","young_lightskinned_female_diffuse3.png"),
        ("woman_c","african-female-young","female_elegantsuit01","braid01","young_darkskinned_female_diffuse.png")]
    for name,target,clothes,hair,skin in profiles:
        print("Preparing",name,flush=True)
        avatar=Avatar(target,name.startswith("woman"));glb=GLB(avatar)
        source_textures=[skin,hair+"_diffuse.png","eyebrow001.png","eyelashes01.png","brown_eye.png","teeth.png"]
        source_textures += [clothes+x for x in ["_diffuse.png","_normal.png","_ao.png"]]
        for texture in source_textures:
            destination=OUTPUT.parent/texture
            if not destination.exists() or not cmp(SOURCE/texture,destination,shallow=False):
                shutil.copyfile(SOURCE/texture,destination)
        skin_mat=glb.material("Skin",skin)
        glb.mesh("Skin",avatar.base,avatar.weights,np.array(avatar.uvs),avatar.skin_faces,skin_mat,True)
        cloth_mat=glb.material("Clothing",clothes+"_diffuse.png",clothes+"_normal.png",clothes+"_ao.png")
        glb.mesh("Clothing",*avatar.fitted(clothes),cloth_mat,True)
        for stem,texture in [(hair,hair+"_diffuse.png"),("eyebrow001","eyebrow001.png"),("eyelashes01","eyelashes01.png")]:
            glb.mesh(stem,*avatar.fitted(stem),glb.material("Hair" if stem==hair else stem,texture,alpha=True))
        glb.mesh("Eyes",*avatar.fitted("high-poly"),glb.material("Eyes","brown_eye.png"),eye=True)
        glb.mesh("Teeth",*avatar.fitted("teeth_base"),glb.material("Teeth","teeth.png"))
        glb.save(OUTPUT/(name+".glb"))



def expressions():
    lines=(SOURCE/"face-poseunits.bvh").read_text().splitlines()
    channels=[];offset=0;name=""
    for line in lines:
        w=line.split()
        if not w:continue
        if w[0] in ["ROOT","JOINT"]:name=w[1]
        elif w[0]=="CHANNELS":channels.append((name,offset,w[2:]));offset+=int(w[1])
    start=next(i for i,line in enumerate(lines) if line.startswith("Frame Time:"))+1
    frames=[list(map(float,line.split())) for line in lines[start:] if line.strip()]
    names=json.loads((SOURCE/"face-poseunits.json").read_text())["framemapping"]
    def quat(axis,angle):return np.r_[np.array(axis)*math.sin(angle/2),math.cos(angle/2)]
    def product(a,b):return np.r_[a[3]*b[:3]+b[3]*a[:3]+np.cross(a[:3],b[:3]),a[3]*b[3]-np.dot(a[:3],b[:3])]
    groups={"Smile":[("MouthLeftPullUp",1),("MouthRightPullUp",1),("LeftCheekUp",.3),("RightCheekUp",.3)],
        "BlinkL":[("LeftUpperLidClosed",1),("LeftLowerLidUp",.25)],
        "BlinkR":[("RightUpperLidClosed",1),("RightLowerLidUp",.25)],"Talk":[("JawDrop",1)],"Pucker":[("LipsKiss",1)]}
    result={}
    for group,units in groups.items():
        angles=defaultdict(lambda:np.zeros(3));totals=defaultdict(float)
        for unit_name,weight in units:
            frame_data=frames[names.index(unit_name)]
            for bone,start,axes in channels:
                euler=np.array([frame_data[start+axes.index(axis+"rotation")] for axis in "XYZ"])
                if np.linalg.norm(euler)<.0001:continue
                angles[bone]+=euler*weight;totals[bone]+=weight
        result[group]={}
        for bone,euler in angles.items():
            x,y,z=np.radians(euler/max(1,totals[bone]))
            # The CC0 BVH uses Z-up coordinates; the game/glTF uses Y-up.
            q=product(product(quat([1,0,0],x),quat([0,0,1],-y)),quat([0,1,0],z))
            result[group][bone.replace(".","_")]=q.tolist()
    (OUTPUT.parent/"expression-poses.json").write_text(json.dumps(result,indent=2)+'\n')
    print("Prepared CC0 facial pose units for smiles, blinking, speaking and sipping.",flush=True)


if __name__=="__main__":
    expressions()
    prepare()
