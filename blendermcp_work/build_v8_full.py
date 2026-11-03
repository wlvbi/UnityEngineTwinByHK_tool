# -*- coding: utf-8 -*-
"""V8 engine - full build per the construction spec.
- X = crank axis, front (accessories) at -X, rear flywheel at +X
- Y = lateral, Z = up
- 90-degree V, each bank 45 deg off vertical, 4 cylinders per bank @ 0.105 spacing
- Internal parts: crankshaft, 8 piston+rod assemblies (correct crank phase),
  camshafts, valves. Animation: crank rotation -> piston/rod reciprocation + explode.
"""
import bpy
import math
from math import radians, sin, cos, pi
from mathutils import Vector

# ============================================================ reset
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
             bpy.data.lights, bpy.data.cameras, bpy.data.objects):
    for item in list(coll):
        if item.users == 0:
            try:
                coll.remove(item)
            except Exception:
                pass

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 240
scene.render.fps = 24

# ============================================================ materials
def make_mat(name, color, metallic=0.0, rough=0.5, bump=0.0, bump_scale=90.0,
             spec=None, coat=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if coat > 0 and "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = 0.18
    if spec is not None and "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = spec
    if bump > 0:
        tex = nt.nodes.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = bump_scale
        tex.inputs['Detail'].default_value = 6.0
        tex.inputs['Roughness'].default_value = 0.65
        bn = nt.nodes.new('ShaderNodeBump')
        bn.inputs['Strength'].default_value = bump
        bn.inputs['Distance'].default_value = 0.004
        nt.links.new(tex.outputs['Fac'], bn.inputs['Height'])
        nt.links.new(bn.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

MAT_ALU_BLOCK = make_mat("alu_block", (0.30, 0.31, 0.33), 0.76, 0.35, bump=0.10, bump_scale=100)
MAT_ALU_HEAD  = make_mat("alu_head",  (0.36, 0.37, 0.39), 0.78, 0.32, bump=0.07, bump_scale=120)
MAT_STEEL     = make_mat("steel_exhaust", (0.52, 0.52, 0.55), 0.90, 0.28, bump=0.07, bump_scale=300)
MAT_CRANK     = make_mat("steel_crank", (0.55, 0.55, 0.58), 0.85, 0.25, bump=0.04, bump_scale=220)
MAT_BLACK     = make_mat("black_plastic", (0.013, 0.013, 0.016), 0.0, 0.42, bump=0.05, spec=0.26, coat=0.30)
MAT_BLKMET    = make_mat("black_metal", (0.025, 0.025, 0.029), 0.9, 0.40, spec=0.3)
MAT_CHROME    = make_mat("chrome", (0.90, 0.90, 0.92), 1.0, 0.08)
MAT_PISTON    = make_mat("piston_alu", (0.72, 0.72, 0.74), 1.0, 0.22)
MAT_GOLD      = make_mat("gold_brass", (0.83, 0.62, 0.18), 1.0, 0.24)
MAT_BLUE      = make_mat("oil_filter_blue", (0.025, 0.13, 0.55), 0.35, 0.32)
MAT_YELLOW    = make_mat("dipstick_yellow", (0.90, 0.68, 0.02), 0.1, 0.34)
MAT_COPPER    = make_mat("copper", (0.52, 0.23, 0.09), 1.0, 0.42)
MAT_RUBBER    = make_mat("rubber", (0.012, 0.012, 0.012), 0.0, 0.65, bump=0.10, bump_scale=300)
MAT_STRIPE    = make_mat("stripe_silver", (0.80, 0.80, 0.82), 1.0, 0.16)
MAT_DGREY     = make_mat("dark_grey", (0.075, 0.075, 0.082), 0.4, 0.45)
MAT_COPPERW   = make_mat("copper_wire", (0.72, 0.36, 0.14), 1.0, 0.35)

def assign(obj, mat):
    if obj.data.materials:
        obj.data.materials.clear()
    obj.data.materials.append(mat)

def smooth(obj):
    if hasattr(obj.data, "polygons"):
        for p in obj.data.polygons:
            p.use_smooth = True
        obj.data.update()

# ============================================================ primitives
def _finish(obj, name, mat, bevel=0.0, seg=2):
    obj.name = name
    if bevel > 0:
        m = obj.modifiers.new("Bevel", 'BEVEL')
        m.width = bevel
        m.segments = seg
        m.limit_method = 'ANGLE'
    assign(obj, mat)
    return obj

def add_cube(name, size, loc, rot=(0, 0, 0), mat=MAT_ALU_BLOCK, bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, name, mat, bevel)

def add_cyl(name, radius, depth, loc, rot=(0, 0, 0), mat=MAT_ALU_BLOCK,
            verts=40, bevel=0.003, smooth_it=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot)
    o = bpy.context.active_object
    if smooth_it:
        smooth(o)
    return _finish(o, name, mat, bevel)

def add_torus(name, major, minor, loc, rot=(0, 0, 0), mat=MAT_BLACK, scale_z=1.0):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     location=loc, rotation=rot,
                                     major_segments=48, minor_segments=12)
    o = bpy.context.active_object
    if scale_z != 1.0:
        o.scale = (1, 1, scale_z)
        bpy.ops.object.transform_apply(scale=True)
    smooth(o)
    return _finish(o, name, mat, 0.0)

def add_cone(name, r1, r2, depth, loc, rot=(0, 0, 0), mat=MAT_ALU_HEAD):
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=r1, radius2=r2,
                                   depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    smooth(o)
    return _finish(o, name, mat, 0.002)

def add_tube(name, points, radius, mat, cyclic=False, res=16, bevel_res=8):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_res
    curve.resolution_u = res
    sp = curve.splines.new('BEZIER')
    sp.bezier_points.add(len(points) - 1)
    for i, p in enumerate(points):
        bp = sp.bezier_points[i]
        bp.co = p
        bp.handle_left_type = 'AUTO'
        bp.handle_right_type = 'AUTO'
    sp.use_cyclic_u = cyclic
    o = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(o)
    assign(o, mat)
    return o

def add_bolt(name, loc, rot=(0, 0, 0), r=0.008, h=0.012, mat=MAT_CHROME):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=r, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    return _finish(o, name, mat, 0.0015)

def cast_surface(obj, strength=0.0016, size=0.045):
    tex = bpy.data.textures.new(obj.name + "_cast", 'CLOUDS')
    tex.noise_scale = size
    tex.noise_depth = 2
    m = obj.modifiers.new("CastDisplace", 'DISPLACE')
    m.texture = tex
    m.strength = strength
    m.mid_level = 0.5
    return obj

# ============================================================ V-layout helpers
BANK = radians(45.0)          # each bank 45 deg off vertical -> 90 deg V
S, C = sin(BANK), cos(BANK)
CRANK_Z = 0.20                # crank axis height
BORE_R = 0.041                # cylinder bore radius (82mm bore)
STROKE = 0.090                # crank stroke (90mm)
ROD_LEN = 0.150               # conrod center-to-center
COMP_H = 0.185                # piston pin height above crank axis at TDC reference
CYL_PITCH = 0.105             # cylinder center spacing along X
BANK_LATERAL = 0.115          # lateral offset of cylinder centerline from valley center

def on_bank(side, d, x=0.0, lat=0.0):
    """d along bank normal (outward), lat across bank width."""
    return (x,
            side * (BANK_LATERAL + S * d + C * lat),
            CRANK_Z + 0.135 + C * d - S * lat)

def bank_rot(side):
    return (radians(135) if side < 0 else radians(45), 0, 0)

def bank_cube(name, side, length, thick_n, width_t, d, x=0.0, lat=0.0,
              mat=MAT_ALU_BLOCK, bevel=0.01):
    bpy.ops.mesh.primitive_cube_add(size=1, location=on_bank(side, d, x, lat),
                                    rotation=bank_rot(side))
    o = bpy.context.active_object
    o.scale = (length, thick_n, width_t)
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, name, mat, bevel)

# cylinder X positions (4 per bank)
CYL_X = [(-1.5 + i) * CYL_PITCH for i in range(4)]   # -0.1575 .. 0.1575

print("PART1 materials/layout OK  CYL_X=%s" % [round(v, 4) for v in CYL_X])

# ============================================================ cylinder block
# main block (the crankcase valley) - 0.42 x 0.36 x 0.28 per spec
cast_surface(add_cube("engine_block", (0.42, 0.36, 0.28), (0, 0, CRANK_Z),
             mat=MAT_ALU_BLOCK, bevel=0.014), 0.0018, 0.05)
# crankcase skirt (opens downward, encloses crank)
add_cube("crankcase_skirt", (0.40, 0.32, 0.12), (0, 0, CRANK_Z - 0.115),
         mat=MAT_ALU_BLOCK, bevel=0.012)
# valley roof (lifter valley between the banks)
add_cube("lifter_valley", (0.36, 0.20, 0.06), (0, 0, CRANK_Z + 0.115),
         mat=MAT_ALU_HEAD, bevel=0.01)

# cylinder bore bosses + actual bore holes (Boolean)
BORE_DEPTH = 0.30
for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate(CYL_X):
        # boss: cylinder liner extruding along bank normal
        center = on_bank(side, 0.145, cx, 0.0)
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=BORE_R + 0.012,
                                            depth=BORE_DEPTH, location=center,
                                            rotation=bank_rot(side))
        boss = bpy.context.active_object
        smooth(boss)
        _finish(boss, f"liner_{tag}{j}", MAT_ALU_HEAD, 0.003)

        # bore hole (cutter) - subtract to leave a dark cylindrical cavity
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=BORE_R,
                                            depth=BORE_DEPTH * 0.94, location=center,
                                            rotation=bank_rot(side))
        cutter = bpy.context.active_object
        cutter.name = f"cutter_{tag}{j}"
        cutter.display_type = 'WIRE'
        cutter.hide_render = True
        # boolean difference on the liner
        bmod = boss.modifiers.new("Bore", 'BOOLEAN')
        bmod.operation = 'DIFFERENCE'
        bmod.object = cutter
        cutter.hide_set(True)

# main bearing webs (5) across the crankcase bottom
for i, mx in enumerate((-0.1575, -0.0788, 0.0, 0.0788, 0.1575)):
    add_cube(f"main_web_{i}", (0.022, 0.26, 0.17), (mx, 0, CRANK_Z - 0.03),
             mat=MAT_ALU_BLOCK, bevel=0.006)
    add_cyl(f"main_bearing_{i}", 0.052, 0.022, (mx, 0, CRANK_Z),
            rot=(0, radians(90), 0), mat=MAT_DGREY)

# machined side rails (silver bands along the flank)
for side in (-1, 1):
    add_cube(f"side_rail_{side}", (0.40, 0.014, 0.040),
             (0, side * 0.183, CRANK_Z + 0.115), mat=MAT_ALU_HEAD, bevel=0.005)
    add_cube(f"side_rail_low_{side}", (0.38, 0.012, 0.032),
             (0, side * 0.181, CRANK_Z - 0.105), mat=MAT_ALU_HEAD, bevel=0.005)

# external casting ribs on both flanks
for side in (-1, 1):
    for i, rx in enumerate((-0.16, -0.08, 0.0, 0.08, 0.16)):
        add_cube(f"block_rib_{side}_{i}", (0.018, 0.012, 0.19),
                 (rx, side * 0.181, CRANK_Z), mat=MAT_ALU_BLOCK, bevel=0.004)
    for i, bx in enumerate((-0.14, 0.0, 0.14)):
        add_bolt(f"block_bolt_{side}_{i}", (bx, side * 0.182, CRANK_Z + 0.05),
                 rot=(0, radians(90), 0), r=0.0072)

# mounting flanges (engine mounts)
for side in (-1, 1):
    add_cube(f"mount_flange_{side}", (0.09, 0.045, 0.10),
             (-0.02, side * 0.195, CRANK_Z - 0.09), mat=MAT_ALU_BLOCK, bevel=0.01)
    add_cyl(f"mount_rubber_{side}", 0.032, 0.04,
            (-0.02, side * 0.222, CRANK_Z - 0.11), rot=(radians(90), 0, 0), mat=MAT_RUBBER)

# timing cover (front) + rear flange
add_cube("timing_cover", (0.05, 0.30, 0.30), (-0.225, 0, CRANK_Z + 0.02),
         mat=MAT_ALU_HEAD, bevel=0.018)
add_cyl("rear_plate", 0.175, 0.05, (0.225, 0, CRANK_Z), rot=(0, radians(90), 0), mat=MAT_ALU_BLOCK)
add_cyl("rear_seal", 0.082, 0.03, (0.252, 0, CRANK_Z), rot=(0, radians(90), 0), mat=MAT_DGREY)
for k in range(8):
    a = k * pi / 4
    add_bolt(f"rear_bolt_{k}", (0.249, 0.145 * cos(a), CRANK_Z + 0.145 * sin(a)),
             rot=(0, radians(90), 0), r=0.007)

# oil pan
add_cube("oil_pan_flange", (0.42, 0.34, 0.018), (0, 0, CRANK_Z - 0.175),
         mat=MAT_ALU_HEAD, bevel=0.005)
add_cube("oil_pan", (0.38, 0.30, 0.08), (0, 0, CRANK_Z - 0.215), mat=MAT_ALU_HEAD, bevel=0.012)
for i, ry in enumerate((-0.09, 0.0, 0.09)):
    add_cube(f"oil_pan_rib_{i}", (0.34, 0.012, 0.016), (0, ry, CRANK_Z - 0.245),
             mat=MAT_ALU_HEAD, bevel=0.004)
for i, bx in enumerate((-0.17, -0.055, 0.06, 0.175)):
    for by in (-0.155, 0.155):
        add_bolt(f"pan_bolt_{i}_{by}", (bx, by, CRANK_Z - 0.172), r=0.0068)

print("PART2 block OK")

# ============================================================ crankshaft
CRANK = bpy.data.objects.new("crank_empty", None)
CRANK.empty_display_size = 0.05
CRANK.location = (0, 0, CRANK_Z)
scene.collection.objects.link(CRANK)

def crank_child(obj):
    obj.parent = CRANK
    obj.matrix_parent_inverse = CRANK.matrix_world.inverted()
    return obj

# front snout + rear flange
crank_child(add_cyl("crank_snout", 0.030, 0.10, (-0.26, 0, CRANK_Z),
                    rot=(0, radians(90), 0), mat=MAT_CRANK))
crank_child(add_cyl("crank_flange", 0.082, 0.035, (0.225, 0, CRANK_Z),
                    rot=(0, radians(90), 0), mat=MAT_CRANK))

# 5 main journals
MAIN_X = [-0.1575, -0.0788, 0.0, 0.0788, 0.1575]
for i, mx in enumerate(MAIN_X):
    crank_child(add_cyl(f"crank_main_{i}", 0.032, 0.048, (mx, 0, CRANK_Z),
                        rot=(0, radians(90), 0), mat=MAT_CRANK))

# cross-plane V8 firing order crank pin phase (deg) for 4 throws
# cross-plane: 0, 90, 270, 180 (common US V8 order spacing)
THROW_PHASE = [0.0, 90.0, 270.0, 180.0]
THROW_R = STROKE / 2.0      # crank pin offset radius = stroke/2 = 0.045
ROD_JOURNAL_X = [-0.118, -0.039, 0.039, 0.118]

crank_pins = []
for k, px in enumerate(ROD_JOURNAL_X):
    ang = radians(THROW_PHASE[k])
    py = THROW_R * sin(ang)
    pz = THROW_R * cos(ang)
    # rod journal
    crank_child(add_cyl(f"crank_pin_{k}", 0.026, 0.052, (px, py, CRANK_Z + pz),
                        rot=(0, radians(90), 0), mat=MAT_CRANK))
    # two crank arms (webs) + counterweights
    for sgn in (-1, 1):
        wx = px + sgn * 0.030
        crank_child(add_cube(f"crank_web_{k}_{sgn}", (0.016, 0.11, 0.024),
                             (wx, py * 0.5, CRANK_Z + pz * 0.5),
                             mat=MAT_CRANK, bevel=0.004))
        # counterweight opposite the pin
        crank_child(add_cube(f"crank_cw_{k}_{sgn}", (0.018, 0.075, 0.115),
                             (wx, -py * 0.85, CRANK_Z - pz * 0.85),
                             mat=MAT_CRANK, bevel=0.012))
    crank_pins.append((px, py, pz, THROW_PHASE[k]))

# ============================================================ pistons + rods (8 sets)
def conrod(name, pin_pos, bank_angle, phase_deg, side):
    """Build one conrod + piston at its crank pin, oriented along the bank normal."""
    px, py, pz = pin_pos
    # piston center: offset from crank pin along bank normal by ROD_LEN
    # bank normal direction for this side (unit vector in Y-Z plane)
    #  bank normal = (0, side*S, C) rotated?  Use on_bank direction:
    ny, nz = side * S, C        # outward-normal unit (approx) for the bank
    piston_c = (px, py + ny * ROD_LEN, CRANK_Z + pz + nz * ROD_LEN)

    # piston body (crown + skirt) oriented along bank
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=BORE_R - 0.003,
                                        depth=0.075, location=piston_c,
                                        rotation=bank_rot(side))
    piston = bpy.context.active_object
    smooth(piston)
    _finish(piston, name + "_piston", MAT_PISTON, 0.004)
    crank_child(piston)

    # 3 piston rings
    for ri in range(3):
        rc = (piston_c[0], piston_c[1] - ny * (0.020 - ri * 0.011),
              piston_c[2] - nz * (0.020 - ri * 0.011))
        bpy.ops.mesh.primitive_torus_add(major_radius=BORE_R - 0.004, minor_radius=0.0022,
                                         location=rc, rotation=bank_rot(side),
                                         major_segments=40, minor_segments=8)
        ring = bpy.context.active_object
        smooth(ring)
        _finish(ring, name + f"_ring{ri}", MAT_DGREY, 0)
        crank_child(ring)

    # piston pin
    pin_loc = (px, py + ny * ROD_LEN, CRANK_Z + pz + nz * ROD_LEN)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.012, depth=0.075,
                                        location=pin_loc,
                                        rotation=(0, radians(90), 0))
    pin = bpy.context.active_object
    smooth(pin)
    _finish(pin, name + "_pin", MAT_CRANK, 0.002)
    crank_child(pin)

    # connecting rod body: between crank pin and piston pin
    mid = ((px + piston_c[0]) / 2, (py + piston_c[1]) / 2, (CRANK_Z + pz + piston_c[2]) / 2)
    # rod axis along (piston_c - crank_pin)
    vx, vy, vz = (piston_c[0] - px, piston_c[1] - py, piston_c[2] - (CRANK_Z + pz))
    length = math.sqrt(vx*vx + vy*vy + vz*vz)
    # orient cylinder along the rod direction
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.011, depth=length,
                                        location=mid)
    rod = bpy.context.active_object
    # rotate cylinder (default along Z) to rod direction
    import mathutils
    zaxis = Vector((0, 0, 1))
    dirv = Vector((vx, vy, vz)).normalized()
    quat = zaxis.rotation_difference(dirv)
    rod.rotation_mode = 'QUATERNION'
    rod.rotation_quaternion = quat
    smooth(rod)
    _finish(rod, name + "_rodbody", MAT_CRANK, 0.002)
    crank_child(rod)

    # big end (crank pin) + small end
    bpy.ops.mesh.primitive_cylinder_add(vertices=28, radius=0.030, depth=0.024,
                                        location=(px, py, CRANK_Z + pz),
                                        rotation=(0, radians(90), 0))
    big = bpy.context.active_object
    smooth(big)
    _finish(big, name + "_bigend", MAT_CRANK, 0.003)
    crank_child(big)
    bpy.ops.mesh.primitive_cylinder_add(vertices=28, radius=0.020, depth=0.022,
                                        location=piston_c, rotation=(0, radians(90), 0))
    small = bpy.context.active_object
    smooth(small)
    _finish(small, name + "_smallend", MAT_CRANK, 0.003)
    crank_child(small)

    # rod bolts
    for sgn in (-1, 1):
        add_bolt(name + f"_bolt{sgn}", (px + sgn * 0.013, py - 0.026 * sin(0), CRANK_Z + pz - 0.026),
                 rot=(0, radians(90), 0), r=0.0045, h=0.02, mat=MAT_CHROME)
        b = bpy.data.objects.get(name + f"_bolt{sgn}")
        if b:
            crank_child(b)

# build 8 conrods: 4 per bank, sharing the 4 crank throws (cross-plane phases)
for side, tag in ((+1, "R"), (-1, "L")):
    for k in range(4):
        px, py, pz, phase = crank_pins[k]
        conrod(f"rod_{tag}{k}", (px, py, pz), BANK, phase, side)

print("PART3 crank+pistons OK")

# ============================================================ heads + valvetrain
COIL_X = [-0.1575, -0.0525, 0.0525, 0.1575]
for side, tag in ((+1, "R"), (-1, "L")):
    # cylinder head casting
    cast_surface(bank_cube(f"head_{tag}", side, 0.42, 0.135, 0.20, 0.30,
                           mat=MAT_ALU_HEAD, bevel=0.012), 0.0014, 0.05)
    # cam shelf band (machined, inboard side of head)
    bank_cube(f"cam_shelf_{tag}", side, 0.40, 0.012, 0.055, 0.315,
              lat=-0.115, mat=MAT_ALU_HEAD, bevel=0.003)
    # machined head deck
    bank_cube(f"head_deck_{tag}", side, 0.415, 0.010, 0.180, 0.243,
              mat=MAT_ALU_HEAD, bevel=0.002)

    # intake + exhaust ports (as tunnels along X per cylinder)
    for j, cx in enumerate(COIL_X):
        add_cyl(f"intake_port_{tag}{j}", 0.023, 0.055,
                (cx, side * 0.125, CRANK_Z + 0.335), rot=(0, radians(90), 0), mat=MAT_DGREY)
        add_cyl(f"exh_port_{tag}{j}", 0.020, 0.045,
                (cx, side * 0.190, CRANK_Z + 0.238), rot=(0, radians(90), 0), mat=MAT_DGREY)
        add_torus(f"exh_gasket_{tag}{j}", 0.0245, 0.0035,
                  (cx, side * 0.212, CRANK_Z + 0.208), rot=bank_rot(side), mat=MAT_DGREY)

    # camshafts (2 per head) running along X
    for cs in (0, 1):
        cy = side * (0.10 + cs * 0.055)
        cz = CRANK_Z + 0.345
        add_cyl(f"cam_{tag}{cs}", 0.017, 0.40, (0, cy, cz),
                rot=(0, radians(90), 0), mat=MAT_CRANK)
        # cam lobes (8 per cam)
        for j, cx in enumerate(COIL_X):
            add_cyl(f"camlobe_{tag}{cs}{j}", 0.022, 0.016,
                    (cx, cy, cz + 0.012), rot=(0, radians(90), 0), mat=MAT_CRANK)
        # cam bearing caps
        for i, cx in enumerate((-0.13, 0.0, 0.13)):
            add_cube(f"camcap_{tag}{cs}{i}", (0.028, 0.020, 0.018),
                     (cx, cy, cz + 0.024), mat=MAT_ALU_HEAD, bevel=0.003)

    # valves: 4 per cylinder (2 intake, 2 exhaust) = 16 per head
    for j, cx in enumerate(COIL_X):
        for vi in range(4):
            vx = cx + (vi - 1.5) * 0.024
            vy = side * (0.115 + (vi % 2) * 0.045)
            vz = CRANK_Z + 0.315
            add_cyl(f"valve_{tag}{j}_{vi}", 0.0085, 0.10, (vx, vy, vz),
                    rot=bank_rot(side), mat=MAT_CRANK)
            # valve spring
            add_torus(f"vspring_{tag}{j}_{vi}", 0.016, 0.004,
                      (vx, vy, vz - 0.03), rot=bank_rot(side), mat=MAT_DGREY)

    # valve cover (black, prominent, with V8 badge + stripes)
    bank_cube(f"valve_cover_{tag}", side, 0.412, 0.062, 0.188, 0.386,
              mat=MAT_BLACK, bevel=0.030)
    bank_cube(f"cover_top_{tag}", side, 0.385, 0.013, 0.170, 0.430,
              mat=MAT_BLACK, bevel=0.010)
    # silver stripes
    for i, lat in enumerate((-0.070, -0.045, -0.020)):
        bank_cube(f"stripe_{tag}{i}", side, 0.26, 0.004, 0.011, 0.442,
                  x=-0.10, lat=lat, mat=MAT_STRIPE, bevel=0.0015)
    # V8 badge
    bpy.ops.object.text_add(location=on_bank(side, 0.445, 0.135, 0.02))
    txt = bpy.context.active_object
    txt.data.body = "V8"
    txt.data.extrude = 0.004
    txt.data.size = 0.085
    txt.data.align_x = 'CENTER'
    txt.data.align_y = 'CENTER'
    txt.rotation_euler = (radians(45) if side < 0 else radians(-45), 0, 0)
    assign(txt, MAT_STRIPE)
    txt.name = f"badge_V8_{tag}"

    # ignition coils (4 per bank) on the cover
    for j, cx in enumerate(COIL_X):
        bank_cube(f"coil_{tag}{j}", side, 0.052, 0.050, 0.040, 0.455,
                  x=cx, lat=-0.055, mat=MAT_BLACK, bevel=0.011)

    # cover bolts
    bi = 0
    for bx in (-0.195, -0.065, 0.065, 0.195):
        for lat in (-0.095, 0.095):
            add_bolt(f"cover_bolt_{tag}{bi}", on_bank(side, 0.438, bx, lat),
                     rot=bank_rot(side), r=0.0075)
            bi += 1

    # fuel rail along inboard head top
    add_cyl(f"fuel_rail_{tag}", 0.013, 0.42, on_bank(side, 0.32, 0, -0.085),
            rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
    for j, cx in enumerate(COIL_X):
        add_cyl(f"injector_{tag}{j}", 0.011, 0.05,
                on_bank(side, 0.30, cx, -0.085), rot=bank_rot(side), mat=MAT_DGREY)

print("PART4 heads/valvetrain OK")

# ============================================================ intake + exhaust
# intake manifold: black central plenum with eight sculpted runners arching across the valley
add_cube("intake_plenum", (0.35, 0.19, 0.115), (0.015, 0, CRANK_Z + 0.505),
         mat=MAT_BLACK, bevel=0.048)
add_cube("plenum_top", (0.29, 0.145, 0.018), (0.015, 0, CRANK_Z + 0.57),
         mat=MAT_BLACK, bevel=0.009)
for i, rx in enumerate((-0.12, -0.06, 0.0, 0.06, 0.12)):
    add_cube(f"plenum_rib_{i}", (0.008, 0.13, 0.006), (rx, 0, CRANK_Z + 0.581),
             mat=MAT_DGREY, bevel=0.002)

# Broad runners rise from each head, arch over the valley and return to the opposite bank.
for i, cx in enumerate((-0.1575, -0.105, -0.0525, 0.0, 0.0525, 0.105, 0.1575, 0.183)):
    x = max(-0.17, min(0.17, cx))
    spread = 0.148 - 0.012 * abs(i - 3.5)
    points = [
        (x, -spread, CRANK_Z + 0.405),
        (x, -spread * 0.92, CRANK_Z + 0.465),
        (x, -0.105, CRANK_Z + 0.535),
        (x, -0.045, CRANK_Z + 0.585),
        (x, 0.025, CRANK_Z + 0.595),
        (x, 0.095, CRANK_Z + 0.555),
        (x, spread * 0.92, CRANK_Z + 0.475),
        (x, spread, CRANK_Z + 0.405),
    ]
    add_tube(f"intake_runner_arch_{i}", points, 0.027, MAT_BLACK, res=20, bevel_res=10)
    for side in (-1, 1):
        add_cyl(f"runner_flange_{i}_{side}", 0.030, 0.010,
                (x, side * spread, CRANK_Z + 0.402), rot=(radians(90), 0, 0), mat=MAT_BLKMET)

# Fuel rails and injector details along the outer intake edges.
for side, tag in ((-1, "L"), (1, "R")):
    add_cyl(f"fuel_rail_{tag}", 0.010, 0.39, (0, side * 0.175, CRANK_Z + 0.425),
            rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
    for j, cx in enumerate(COIL_X):
        add_cyl(f"injector_{tag}{j}", 0.009, 0.045,
                (cx, side * 0.166, CRANK_Z + 0.407), rot=(radians(25), 0, 0), mat=MAT_DGREY)

add_tube("coolant_crossover", [(-0.185, -0.115, CRANK_Z + 0.30),
                               (-0.12, -0.145, CRANK_Z + 0.265),
                               (0.05, -0.145, CRANK_Z + 0.255),
                               (0.185, -0.115, CRANK_Z + 0.265)],
         0.016, MAT_ALU_HEAD)
add_cyl("coolant_neck", 0.030, 0.10, (-0.175, -0.175, CRANK_Z + 0.345),
        rot=(radians(-40), radians(-12), 0), mat=MAT_ALU_HEAD)
add_torus("coolant_neck_lip", 0.031, 0.007, (-0.205, -0.215, CRANK_Z + 0.395),
          rot=(radians(-40), radians(-12), 0), mat=MAT_ALU_HEAD)
for side, tag in ((-1, "L"), (1, "R")):
    add_tube(f"pcv_hose_{tag}", [
        (0.135, side * 0.10, CRANK_Z + 0.47),
        (0.185, side * 0.10, CRANK_Z + 0.505),
        (0.205, side * 0.055, CRANK_Z + 0.53)], 0.010, MAT_RUBBER)

# throttle body at the front end of the intake plenum
TB_X, TB_Y, TB_Z = -0.215, -0.018, CRANK_Z + 0.505
add_cyl("throttle_body", 0.067, 0.108, (TB_X, TB_Y, TB_Z), rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
add_cyl("throttle_bore", 0.052, 0.008, (TB_X - 0.055, TB_Y, TB_Z), rot=(0, radians(90), 0), mat=MAT_DGREY)
add_cyl("throttle_butterfly", 0.047, 0.009, (TB_X - 0.061, TB_Y, TB_Z),
        rot=(0, radians(90), radians(18)), mat=MAT_GOLD)
add_torus("throttle_lip", 0.063, 0.007, (TB_X - 0.065, TB_Y, TB_Z),
          rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
add_cube("throttle_actuator", (0.065, 0.080, 0.065), (TB_X - 0.006, 0.088, TB_Z - 0.006),
         mat=MAT_BLACK, bevel=0.012)

# exhaust headers: 4 tubes per bank -> collector
for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate(COIL_X):
        pts = [
            (cx, side * 0.208, CRANK_Z + 0.212),
            (cx, side * 0.300, CRANK_Z + 0.140),
            (cx + 0.012, side * 0.342, CRANK_Z + 0.062),
            (0.15 + j * 0.016, side * 0.352, CRANK_Z - 0.030),
            (0.212, side * 0.340, CRANK_Z - 0.095),
        ]
        add_tube(f"header_{tag}{j}", pts, 0.0205, MAT_STEEL)
    add_cone(f"header_merge_{tag}", 0.056, 0.041, 0.075,
             (0.252, side * 0.342, CRANK_Z - 0.100), rot=(0, radians(-90), 0), mat=MAT_STEEL)
    add_cyl(f"header_collector_{tag}", 0.041, 0.15,
            (0.292, side * 0.342, CRANK_Z - 0.100), rot=(0, radians(90), 0), mat=MAT_STEEL)
    add_cyl(f"header_outlet_{tag}", 0.030, 0.09,
            (0.375, side * 0.342, CRANK_Z - 0.100), rot=(0, radians(90), 0), mat=MAT_STEEL)

print("PART5 intake/exhaust OK")

# ============================================================ accessory drive
PC = -0.285            # pulley center plane along X
BELT_X = -0.312        # belt plane

def convex_hull(points):
    pts = sorted(set(points))
    if len(pts) <= 2:
        return pts
    def cr(o, a, b):
        return (a[0]-o[0])*(b[1]-o[1]) - (a[1]-o[1])*(b[0]-o[0])
    lower = []
    for p in pts:
        while len(lower) >= 2 and cr(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cr(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]

def pulley(name, y, z, r, depth=0.05, mat=MAT_BLKMET, hub=True):
    add_cyl(name, r, depth, (PC, y, z), rot=(0, radians(90), 0), mat=mat)
    for gi in range(3):
        gr = r * (0.76 + gi * 0.075)
        add_torus(name + f"_groove{gi}", gr, 0.0022,
                  (PC - depth * 0.36, y, z), rot=(0, radians(90), 0), mat=MAT_DGREY)
    if hub:
        add_cyl(name + "_hub", r * 0.30, depth + 0.028, (PC - 0.008, y, z),
                rot=(0, radians(90), 0), mat=MAT_CHROME)

PCIRCLES = [(0.0, CRANK_Z, 0.105), (0.0, CRANK_Z + 0.20, 0.072),
            (-0.235, CRANK_Z + 0.335, 0.048), (-0.128, CRANK_Z + 0.115, 0.038),
            (0.100, CRANK_Z + 0.255, 0.034), (0.112, CRANK_Z + 0.155, 0.036),
            (-0.112, CRANK_Z - 0.005, 0.036), (-0.225, CRANK_Z - 0.035, 0.055)]

for (py, pz, pr) in PCIRCLES:
    pulley("pulley_%.3f_%.3f" % (py, pz), py, pz, pr)

# crank hub
add_cyl("crank_hub", 0.036, 0.06, (PC + 0.02, 0, CRANK_Z), rot=(0, radians(90), 0), mat=MAT_CHROME)
# water pump housing
add_cyl("waterpump_housing", 0.085, 0.075, (-0.225, 0, CRANK_Z + 0.20), rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
# alternator (copper core + silver caps)
ALT_Y, ALT_Z = -0.235, CRANK_Z + 0.335
add_cyl("alt_rear", 0.082, 0.05, (-0.240, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
add_cyl("alt_core", 0.080, 0.078, (-0.282, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_COPPER)
add_cyl("alt_front", 0.066, 0.040, (-0.322, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_ALU_HEAD)
add_cyl("alt_hub", 0.026, 0.050, (-0.345, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_DGREY)
for k in range(12):
    a = 2 * pi * k / 12
    y0, z0 = ALT_Y + 0.050 * cos(a), ALT_Z + 0.050 * sin(a)
    y1, z1 = ALT_Y + 0.076 * cos(a), ALT_Z + 0.076 * sin(a)
    add_tube(f"alt_vent_{k}", [(-0.335, y0, z0), (-0.332, y1, z1)], 0.0045, MAT_DGREY, res=8, bevel_res=4)
    add_cube(f"alt_slot_{k}", (0.008, 0.010, 0.034),
             (-0.301, ALT_Y + 0.061 * cos(a), ALT_Z + 0.061 * sin(a)),
             mat=MAT_DGREY, bevel=0.003)
# AC compressor (near side)
add_cyl("ac_compressor", 0.072, 0.13, (-0.275, -0.225, CRANK_Z - 0.035), rot=(0, radians(90), 0), mat=MAT_BLKMET)

# serpentine belt (convex hull ring)
outer, inner = [], []
for (py, pz, pr) in PCIRCLES:
    ro, ri = pr + 0.0035, pr - 0.0045
    for k in range(144):
        a = 2 * pi * k / 144
        outer.append((round(py + ro * cos(a), 6), round(pz + ro * sin(a), 6)))
        inner.append((round(py + ri * cos(a), 6), round(pz + ri * sin(a), 6)))
pairs = dict(zip(outer, inner))
hull = convex_hull(outer)
ring_in = [pairs[p] for p in hull]
bm = bpy.data.meshes.new("belt_mesh")
n = len(hull)
verts = [(BELT_X, y, z) for (y, z) in hull] + [(BELT_X, y, z) for (y, z) in ring_in]
faces = [[i, (i+1) % n, n + (i+1) % n, n + i] for i in range(n)]
bm.from_pydata(verts, [], faces)
bm.update()
belt = bpy.data.objects.new("serpentine_belt", bm)
scene.collection.objects.link(belt)
assign(belt, MAT_RUBBER)
sol = belt.modifiers.new("Solidify", 'SOLIDIFY')
sol.thickness = 0.020

# wiring harness along the far bank top
add_tube("wiring_harness", [(-0.185, 0.135, CRANK_Z + 0.52),
                            (0.0, 0.155, CRANK_Z + 0.535),
                            (0.175, 0.135, CRANK_Z + 0.50)],
         0.011, MAT_RUBBER)

# oil filter (blue) + dipstick (yellow)
add_cyl("oil_filter", 0.043, 0.11, (0.03, -0.265, CRANK_Z - 0.175),
        rot=(radians(115), 0, 0), mat=MAT_BLUE)
add_cyl("dipstick_tube", 0.0065, 0.30, (0.075, -0.245, CRANK_Z + 0.175),
        rot=(radians(22), radians(-6), 0), mat=MAT_DGREY)
add_torus("dipstick_handle", 0.030, 0.0055, (0.10, -0.300, CRANK_Z + 0.335),
          rot=(radians(68), 0, radians(12)), mat=MAT_YELLOW)

print("PART6 accessories OK  hull=%d" % len(hull))

# ============================================================ studio + camera + lights
bpy.ops.mesh.primitive_plane_add(size=250, location=(0, 0, CRANK_Z - 0.30))
floor = bpy.context.active_object
floor.name = "studio_floor"
assign(floor, make_mat("floor_mat", (0.80, 0.80, 0.81), 0.0, 0.42))

world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next((n for n in world.node_tree.nodes if n.type == 'BACKGROUND'), None)
if bg is None:
    bg = world.node_tree.nodes.new('ShaderNodeBackground')
    out = world.node_tree.nodes.new('ShaderNodeOutputWorld')
    world.node_tree.links.new(bg.outputs['Background'], out.inputs['Surface'])
bg.inputs[0].default_value = (0.83, 0.84, 0.86, 1.0)
bg.inputs[1].default_value = 0.46

bpy.ops.object.empty_add(location=(-0.01, 0, CRANK_Z + 0.15))
aim = bpy.context.active_object
aim.name = "cam_target"

def add_area(name, loc, energy, size, color=(1, 1, 1)):
    d = bpy.data.lights.new(name, 'AREA')
    d.energy = energy
    d.shape = 'RECTANGLE'
    d.size = size
    d.size_y = size * 0.6
    d.color = color
    o = bpy.data.objects.new(name, d)
    scene.collection.objects.link(o)
    o.location = loc
    con = o.constraints.new('TRACK_TO')
    con.target = aim
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    return o

add_area("key", (-1.95, -1.7, CRANK_Z + 1.9), 420, 2.6)
add_area("fill", (1.9, -1.15, CRANK_Z + 1.1), 80, 3.0, (0.96, 0.97, 1.0))
add_area("rim", (0.35, 2.2, CRANK_Z + 1.5), 240, 2.2, (1.0, 0.99, 0.97))
add_area("top", (0.0, 0.35, CRANK_Z + 2.8), 75, 3.2)

cam_data = bpy.data.cameras.new("Camera")
cam_data.lens = 55
cam = bpy.data.objects.new("Camera", cam_data)
scene.collection.objects.link(cam)
cam.location = (-1.58, -1.72, CRANK_Z + 0.72)
tc = cam.constraints.new('TRACK_TO')
tc.target = aim
tc.track_axis = 'TRACK_NEGATIVE_Z'
tc.up_axis = 'UP_Y'
scene.camera = cam

# ============================================================ render settings (Cycles)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 128
scene.cycles.use_denoising = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 1280
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -0.10

# ============================================================ animation
# 1) crank rotation -> piston/rod reciprocation (children follow crank_empty)
# cross-plane crank: 2 revolutions per engine cycle; animate crank rotation
import mathutils
CRANK.rotation_mode = 'XYZ'
for frame, deg in ((1, 0.0), (240, 720.0)):
    CRANK.rotation_euler = (radians(deg), 0, 0)
    CRANK.keyframe_insert(data_path="rotation_euler", frame=frame)
# linear interpolation (Blender 5.x Action API)
def set_linear(obj):
    ad = obj.animation_data
    if not ad or not ad.action:
        return
    act = ad.action
    fcs = []
    if hasattr(act, "fcurves"):
        fcs = list(act.fcurves)
    else:
        try:
            for layer in act.layers:
                for strip in layer.strips:
                    for cb in strip.channelbags:
                        fcs.extend(list(cb.fcurves))
        except Exception:
            pass
    for fc in fcs:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'

set_linear(CRANK)

# 2) camshaft rotation (half crank speed)
for o in bpy.data.objects:
    if o.name.startswith("cam_") and o.type == 'MESH':
        o.rotation_mode = 'XYZ'
        for frame, deg in ((1, 0.0), (240, 360.0)):
            o.rotation_euler = (0, radians(deg), 0)
            o.keyframe_insert(data_path="rotation_euler", frame=frame)
        set_linear(o)

# 3) explode / assemble: frame 1 assembled -> 120 exploded -> 240 assembled
EXPLODE = {
    "crank": ((0, 0, -0.30),),
    "rod_": ((0, 0, 0),),
    "piston": ((0, 0, 0),),
    "head_": ((0, 0, 0.26),),
    "valve_cover": ((0, 0, 0.36),),
    "cover_top": ((0, 0, 0.36),),
    "stripe_": ((0, 0, 0.36),),
    "badge_": ((0, 0, 0.36),),
    "coil_": ((0, 0, 0.42),),
    "intake_": ((0, 0, 0.46),),
    "plenum": ((0, 0, 0.46),),
    "runner_": ((0, 0, 0.46),),
    "throttle": ((-0.22, 0, 0.34),),
    "header_": ((0, 0, -0.24),),
    "exh_": ((0, 0, -0.24),),
    "oil_pan": ((0, 0, -0.36),),
    "pulley_": ((-0.30, 0, 0),),
    "serpentine": ((-0.42, 0, 0),),
    "alt_": ((-0.22, -0.22, 0.18),),
    "ac_": ((-0.22, 0.22, -0.12),),
    "crank_": ((0, 0, -0.30),),
    "cam_": ((0, 0, 0.22),),
    "valve_": ((0, 0, 0.22),),
    "main_": ((0, 0, -0.18),),
}
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    vec = None
    for key, v in EXPLODE.items():
        if o.name.startswith(key):
            vec = v[0]
            break
    if vec is None:
        continue
    base = o.location.copy()
    for frame, f in ((1, 0.0), (120, 1.0), (240, 0.0)):
        o.location = (base[0] + vec[0] * f, base[1] + vec[1] * f, base[2] + vec[2] * f)
        o.keyframe_insert(data_path="location", frame=frame)
    o.location = base

scene.frame_set(1)
print("PART7 studio/anim OK  total_objs=%d" % len(bpy.data.objects))
scene.render.filepath = "D:/UnityProgram/UnityShader/blendermcp_work/v8_reference_render.png"
bpy.ops.wm.save_as_mainfile(filepath="D:/UnityProgram/UnityShader/blendermcp_work/v8_reference.blend")
bpy.ops.render.render(write_still=True)
print("FINAL saved D:/UnityProgram/UnityShader/blendermcp_work/v8_reference.blend and rendered D:/UnityProgram/UnityShader/blendermcp_work/v8_reference_render.png")
