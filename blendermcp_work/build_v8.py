# -*- coding: utf-8 -*-
"""LS-style V8 engine matching the reference photo.
Fixed bank placement: proper 90-degree V layout.
X = crank axis (front accessories -X), Y = width, Z = up.
"""
import bpy
import math
from math import radians, sin, cos
from mathutils import Matrix

# ----------------------------------------------------------------------------
# reset
# ----------------------------------------------------------------------------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
    for item in list(block):
        if item.users == 0:
            block.remove(item)

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 120
scene.render.fps = 24

# ----------------------------------------------------------------------------
# materials
# ----------------------------------------------------------------------------
def make_mat(name, color, metallic=0.0, rough=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        bsdf = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        out = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    # tame grazing-angle specular wash-out on dark plastics
    if "Specular IOR Level" in bsdf.inputs and rough > 0.35 and metallic < 0.5:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    return mat

MAT_CAST   = make_mat("alu_cast",    (0.76, 0.76, 0.78), 1.0, 0.55)
MAT_ALU    = make_mat("alu_machined",(0.88, 0.88, 0.90), 1.0, 0.32)
MAT_BLACK  = make_mat("black_plastic",(0.022, 0.022, 0.025), 0.0, 0.42)
MAT_BLKMET = make_mat("black_metal", (0.035, 0.035, 0.04), 0.85, 0.40)
MAT_CHROME = make_mat("chrome",      (0.92, 0.92, 0.94), 1.0, 0.18)
MAT_GOLD   = make_mat("gold_brass",  (0.82, 0.62, 0.22), 1.0, 0.28)
MAT_BLUE   = make_mat("oil_filter_blue", (0.04, 0.18, 0.72), 0.25, 0.38)
MAT_YELLOW = make_mat("dipstick_yellow", (0.92, 0.76, 0.04), 0.1, 0.35)
MAT_COPPER = make_mat("copper",      (0.72, 0.36, 0.16), 1.0, 0.35)
MAT_RUBBER = make_mat("belt_rubber", (0.02, 0.02, 0.02), 0.0, 0.72)
MAT_STRIPE = make_mat("stripe_silver", (0.85, 0.85, 0.87), 1.0, 0.22)
MAT_DGREY  = make_mat("dark_grey",   (0.10, 0.10, 0.11), 0.3, 0.55)

def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)

def smooth(obj):
    if hasattr(obj.data, "polygons"):
        obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
        obj.data.update()

parts = {}
def register(name, obj, explode):
    obj.name = name
    parts[name] = (obj, explode)
    return obj

# ----------------------------------------------------------------------------
# primitives
# ----------------------------------------------------------------------------
def add_cube(name, size, loc, rot=(0, 0, 0), mat=MAT_CAST, bevel=0.008, explode=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        m = obj.modifiers.new("Bevel", 'BEVEL')
        m.width = bevel
        m.segments = 2
        m.limit_method = 'ANGLE'
    assign(obj, mat)
    return register(name, obj, explode)

def add_cyl(name, radius, depth, loc, rot=(0, 0, 0), mat=MAT_CAST, verts=32,
            bevel=0.004, explode=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot)
    obj = bpy.context.active_object
    if bevel > 0:
        m = obj.modifiers.new("Bevel", 'BEVEL')
        m.width = bevel
        m.segments = 2
        m.limit_method = 'ANGLE'
    assign(obj, mat)
    smooth(obj)
    return register(name, obj, explode)

def add_torus(name, major, minor, loc, rot=(0, 0, 0), mat=MAT_BLACK, explode=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     location=loc, rotation=rot,
                                     major_segments=40, minor_segments=12)
    obj = bpy.context.active_object
    assign(obj, mat)
    smooth(obj)
    return register(name, obj, explode)

def add_curve_tube(name, points, radius, mat, explode=(0, 0, 0), cyclic=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = radius
    curve.bevel_resolution = 6
    curve.resolution_u = 12
    spline = curve.splines.new('BEZIER')
    spline.bezier_points.add(len(points) - 1)
    for i, p in enumerate(points):
        bp = spline.bezier_points[i]
        bp.co = p
        bp.handle_left_type = 'AUTO'
        bp.handle_right_type = 'AUTO'
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(obj)
    assign(obj, mat)
    return register(name, obj, explode)

# ----------------------------------------------------------------------------
# bank coordinate helpers  (90-degree V, 45 degrees per bank)
# ----------------------------------------------------------------------------
BANK = radians(45)
CRANK_Z = 0.20
VALVE_Z = 0.34                       # valley top reference
S, C = sin(BANK), cos(BANK)

def bank_rot(side):
    return (-side * BANK, 0, 0)

def on_bank(side, dist, x=0.0, lat=0.0):
    """Point `dist` along bank normal from valley origin, `lat` across bank width
    (lat>0 = outward/down the bank slope, symmetric on both banks)."""
    y = side * (S * dist + C * lat)
    z = VALVE_Z + C * dist - S * lat
    return (x, y, z)

# ----------------------------------------------------------------------------
# block / crankcase / valley
# ----------------------------------------------------------------------------
add_cube("engine_block", (0.54, 0.32, 0.21), (0, 0, CRANK_Z - 0.01),
         mat=MAT_CAST, bevel=0.012, explode=(0, 0, -0.10))
add_cube("block_skirt", (0.52, 0.29, 0.10), (0, 0, CRANK_Z - 0.12),
         mat=MAT_CAST, bevel=0.01, explode=(0, 0, -0.10))
add_cube("block_valley", (0.48, 0.17, 0.09), (0, 0, VALVE_Z - 0.03),
         mat=MAT_CAST, bevel=0.01, explode=(0, 0, -0.10))

# ----------------------------------------------------------------------------
# banks: base + head + valve cover + stripes + V8 badge + coils
# ----------------------------------------------------------------------------
for side, tag in ((+1, "R"), (-1, "L")):
    rot = bank_rot(side)

    # bank base block (the V walls)
    add_cube(f"bank_base_{tag}", (0.55, 0.20, 0.15), on_bank(side, 0.055),
             rot, mat=MAT_CAST, bevel=0.012, explode=(0, side * 0.30, 0.18))

    # cylinder head
    head_c = on_bank(side, 0.185)
    add_cube(f"cylinder_head_{tag}", (0.52, 0.165, 0.115), head_c, rot,
             mat=MAT_CAST, bevel=0.01, explode=(0, side * 0.42, 0.30))
    # machined top face of head (thin slab)
    add_cube(f"head_rail_{tag}", (0.53, 0.155, 0.018), on_bank(side, 0.252),
             rot, mat=MAT_ALU, bevel=0.004, explode=(0, side * 0.42, 0.30))

    # valve cover (black, prominent)
    cover_c = on_bank(side, 0.335)
    add_cube(f"valve_cover_{tag}", (0.55, 0.185, 0.078), cover_c, rot,
             mat=MAT_BLACK, bevel=0.022, explode=(0, side * 0.55, 0.42))
    # silver stripes along cover front/mid section (3 lines, clear of the badge area)
    for i, lat in enumerate((-0.055, 0.0, 0.055)):
        p = on_bank(side, 0.378, x=-0.045, lat=lat)
        add_cube(f"cover_stripe_{tag}{i}", (0.40, 0.018, 0.008), p, rot,
                 mat=MAT_STRIPE, bevel=0.002, explode=(0, side * 0.55, 0.42))

    # "V8" badge on near (left) cover, rear clear area like the reference
    if tag == "L":
        bpy.ops.object.text_add()
        txt = bpy.context.active_object
        txt.data.body = "V8"
        txt.data.extrude = 0.006
        txt.data.size = 0.082
        txt.data.align_x = 'CENTER'
        txt.data.align_y = 'CENTER'
        txt.location = on_bank(side, 0.384, x=0.215, lat=0.0)
        # bank tilt lays text flat on the cover face; baseline runs along X
        txt.rotation_euler = (rot[0], 0, 0)
        assign(txt, MAT_STRIPE)
        register("badge_V8", txt, (0, side * 0.55, 0.42))

    # ignition coils: 4 per bank on the outer top of the cover
    for j, cx in enumerate((-0.185, -0.065, 0.055, 0.175)):
        add_cube(f"coil_{tag}{j+1}", (0.058, 0.05, 0.042),
                 on_bank(side, 0.395, x=cx, lat=0.058), rot,
                 mat=MAT_BLACK, bevel=0.012, explode=(0, side * 0.72, 0.55))
        add_cyl(f"coil_nub_{tag}{j+1}", 0.011, 0.022,
                on_bank(side, 0.42, x=cx, lat=0.058), rot,
                mat=MAT_DGREY, explode=(0, side * 0.72, 0.55))

    # fuel rail (silver) along inner top of head, under the runners
    add_cyl(f"fuel_rail_{tag}", 0.014, 0.46, on_bank(side, 0.275, lat=-0.055),
            rot=(0, radians(90), 0), mat=MAT_ALU,
            explode=(0, side * 0.40, 0.52))
    for j, cx in enumerate((-0.185, -0.065, 0.055, 0.175)):
        add_cyl(f"injector_{tag}{j+1}", 0.012, 0.05,
                on_bank(side, 0.258, x=cx, lat=-0.055), rot,
                mat=MAT_DGREY, explode=(0, side * 0.40, 0.52))

# ----------------------------------------------------------------------------
# intake manifold: plenum + 8 black runners + throttle body
# ----------------------------------------------------------------------------
add_cube("intake_plenum", (0.40, 0.23, 0.105), (0.01, 0, 0.70),
         mat=MAT_BLACK, bevel=0.022, explode=(0, 0, 0.48))
add_cube("intake_plenum_top", (0.33, 0.17, 0.03), (0.01, 0, 0.762),
         mat=MAT_BLACK, bevel=0.014, explode=(0, 0, 0.48))

for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate((-0.185, -0.065, 0.055, 0.175)):
        # runners arc over the valley and dive into the head's top-inner face,
        # staying inboard of the valve cover so the cover top stays visible
        pts = [
            (cx * 0.7 + 0.01, side * 0.10, 0.72),
            (cx * 0.9, side * 0.16, 0.69),
            (cx, side * 0.175, 0.645),
            (cx, side * 0.135, 0.595),
            (cx, side * 0.118, 0.565),
        ]
        add_curve_tube(f"runner_{tag}{j+1}", pts, 0.029, MAT_BLACK,
                       explode=(0, side * 0.30, 0.48))
        add_cyl(f"runner_flange_{tag}{j+1}", 0.036, 0.016,
                (cx, side * 0.112, 0.562), rot=(radians(90), 0, 0),
                mat=MAT_BLACK, explode=(0, side * 0.30, 0.48))

# throttle body at front of plenum
add_cyl("throttle_body", 0.075, 0.115, (-0.255, 0, 0.70),
        rot=(0, radians(90), 0), mat=MAT_ALU, explode=(-0.38, 0, 0.30))
# butterfly valve: tilted so the gold disc reads from the camera
add_cyl("throttle_butterfly", 0.070, 0.008, (-0.312, 0, 0.70),
        rot=(0, radians(90), radians(28)), mat=MAT_GOLD, explode=(-0.38, 0, 0.30))
add_torus("throttle_ring", 0.075, 0.009, (-0.318, 0, 0.70),
          rot=(0, radians(90), 0), mat=MAT_ALU, explode=(-0.38, 0, 0.30))
add_cube("throttle_actuator", (0.055, 0.085, 0.065), (-0.25, -0.105, 0.70),
         mat=MAT_BLACK, bevel=0.01, explode=(-0.38, -0.18, 0.30))

# ----------------------------------------------------------------------------
# exhaust headers (4 tubes per bank -> collector)
# ----------------------------------------------------------------------------
for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate((-0.185, -0.065, 0.055, 0.175)):
        pts = [
            (cx, side * 0.225, 0.445),
            (cx, side * 0.335, 0.415),
            (cx + 0.015, side * 0.395, 0.325),
            (0.10 + j * 0.025, side * 0.415, 0.245),
            (0.265, side * 0.40, 0.215),
        ]
        add_curve_tube(f"header_{tag}{j+1}", pts, 0.021, MAT_ALU,
                       explode=(0, side * 0.58, -0.05))
    add_cyl(f"header_collector_{tag}", 0.046, 0.15, (0.325, side * 0.40, 0.215),
            rot=(0, radians(90), 0), mat=MAT_ALU,
            explode=(0.14, side * 0.58, -0.05))

# ----------------------------------------------------------------------------
# oil pan + ribs
# ----------------------------------------------------------------------------
add_cube("oil_pan", (0.52, 0.33, 0.115), (0, 0, 0.075),
         mat=MAT_ALU, bevel=0.012, explode=(0, 0, -0.45))
add_cube("oil_pan_flange", (0.555, 0.36, 0.02), (0, 0, 0.132),
         mat=MAT_ALU, bevel=0.006, explode=(0, 0, -0.45))
for i, ry in enumerate((-0.105, 0.0, 0.105)):
    add_cube(f"oil_pan_rib{i}", (0.44, 0.016, 0.018), (0, ry, 0.026),
             mat=MAT_ALU, bevel=0.004, explode=(0, 0, -0.45))

# ----------------------------------------------------------------------------
# oil filter (blue), dipstick (yellow), coolant pipe  — near (-Y) side like reference
# ----------------------------------------------------------------------------
add_cyl("oil_filter", 0.038, 0.11, (0.225, -0.275, 0.135),
        rot=(radians(15), 0, 0), mat=MAT_BLUE, explode=(0.10, -0.45, -0.15))
add_cyl("oil_filter_base", 0.030, 0.028, (0.225, -0.245, 0.188),
        rot=(radians(15), 0, 0), mat=MAT_DGREY, explode=(0.10, -0.45, -0.15))

add_cyl("dipstick_tube", 0.0065, 0.28, (0.115, -0.255, 0.315),
        rot=(radians(6), 0, 0), mat=MAT_DGREY, explode=(0.05, -0.40, 0.15))
add_torus("dipstick_handle", 0.025, 0.005, (0.115, -0.272, 0.455),
          rot=(radians(-84), 0, 0), mat=MAT_YELLOW, explode=(0.05, -0.40, 0.15))

add_cyl("coolant_pipe", 0.028, 0.125, (-0.235, -0.235, 0.545),
        rot=(radians(25), radians(-15), 0), mat=MAT_ALU, explode=(-0.15, -0.32, 0.35))
add_torus("coolant_pipe_lip", 0.029, 0.006, (-0.268, -0.258, 0.60),
          rot=(radians(25), radians(-15), 0), mat=MAT_ALU, explode=(-0.15, -0.32, 0.35))

# ----------------------------------------------------------------------------
# front accessory drive (all pulley faces aligned to the belt plane)
# ----------------------------------------------------------------------------
FX = -0.40
BELT_X = FX - 0.105                      # belt wraps the pulley front faces

add_cyl("crank_pulley", 0.115, 0.05, (BELT_X + 0.026, 0, CRANK_Z),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.45, 0, -0.12))
add_cyl("crank_pulley_hub", 0.036, 0.024, (BELT_X - 0.012, 0, CRANK_Z),
        rot=(0, radians(90), 0), mat=MAT_CHROME, explode=(-0.45, 0, -0.12))

add_cyl("waterpump_pulley", 0.075, 0.04, (BELT_X + 0.02, 0, CRANK_Z + 0.22),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.45, 0, 0.20))
add_cyl("waterpump_hub", 0.023, 0.02, (BELT_X - 0.012, 0, CRANK_Z + 0.22),
        rot=(0, radians(90), 0), mat=MAT_CHROME, explode=(-0.45, 0, 0.20))

add_cyl("tensioner_pulley", 0.048, 0.03, (BELT_X + 0.016, -0.125, CRANK_Z + 0.10),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.40, -0.35, 0.02))
add_cyl("idler_pulley", 0.042, 0.028, (BELT_X + 0.015, 0.155, CRANK_Z + 0.155),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.40, 0.35, 0.08))

# alternator upper-left: silver caps with visible copper core band
ALT_Y, ALT_Z = -0.275, CRANK_Z + 0.315
add_cyl("alternator_rear", 0.084, 0.05, (FX + 0.075, ALT_Y, ALT_Z),
        rot=(0, radians(90), 0), mat=MAT_ALU, explode=(-0.32, -0.55, 0.32))
add_cyl("alternator_core", 0.088, 0.085, (FX + 0.01, ALT_Y, ALT_Z),
        rot=(0, radians(90), 0), mat=MAT_COPPER, explode=(-0.32, -0.55, 0.32))
add_cyl("alternator_front", 0.084, 0.05, (FX - 0.055, ALT_Y, ALT_Z),
        rot=(0, radians(90), 0), mat=MAT_ALU, explode=(-0.32, -0.55, 0.32))
add_cyl("alternator_pulley", 0.052, 0.034, (BELT_X + 0.014, ALT_Y, ALT_Z),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.32, -0.55, 0.32))
for k in range(6):
    ang = k * math.pi / 3
    add_cube(f"alt_vent{k}", (0.012, 0.028, 0.015),
             (FX - 0.082, ALT_Y + 0.070 * cos(ang), ALT_Z + 0.070 * sin(ang)),
             mat=MAT_DGREY, bevel=0.002, explode=(-0.32, -0.55, 0.32))

# AC compressor lower right
add_cyl("ac_compressor", 0.072, 0.11, (FX + 0.015, 0.255, CRANK_Z - 0.02),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.32, 0.55, -0.18))
add_cyl("ac_pulley", 0.052, 0.03, (BELT_X + 0.016, 0.255, CRANK_Z - 0.02),
        rot=(0, radians(90), 0), mat=MAT_BLKMET, explode=(-0.32, 0.55, -0.18))

# timing cover + water pump housing fill the block front face
add_cube("timing_cover", (0.045, 0.30, 0.24), (-0.285, 0, 0.22),
         mat=MAT_ALU, bevel=0.014, explode=(-0.18, 0, -0.05))
add_cyl("waterpump_housing", 0.088, 0.075, (-0.315, 0, CRANK_Z + 0.22),
        rot=(0, radians(90), 0), mat=MAT_ALU, explode=(-0.30, 0, 0.20))

# serpentine belt: closed loop hugging each pulley arc
BX = BELT_X - 0.012
belt_pts = [
    (BX, -0.275, 0.567), (BX, -0.312, 0.542), (BX, -0.327, 0.515), (BX, -0.312, 0.478),
    (BX, -0.24, 0.38), (BX, -0.185, 0.32),
    (BX, -0.173, 0.30), (BX, -0.16, 0.275), (BX, -0.125, 0.252), (BX, -0.09, 0.265),
    (BX, -0.088, 0.23), (BX, -0.105, 0.20),
    (BX, -0.082, 0.12), (BX, -0.04, 0.088), (BX, 0.0, 0.085), (BX, 0.04, 0.088), (BX, 0.082, 0.12),
    (BX, 0.105, 0.20), (BX, 0.14, 0.16), (BX, 0.20, 0.135),
    (BX, 0.255, 0.128), (BX, 0.292, 0.152), (BX, 0.307, 0.18), (BX, 0.292, 0.217),
    (BX, 0.26, 0.27), (BX, 0.22, 0.32),
    (BX, 0.197, 0.355), (BX, 0.185, 0.385), (BX, 0.155, 0.397),
    (BX, 0.12, 0.415), (BX, 0.075, 0.42), (BX, 0.04, 0.455), (BX, 0.0, 0.495),
    (BX, -0.04, 0.455), (BX, -0.075, 0.42),
    (BX, -0.13, 0.455), (BX, -0.19, 0.51),
]
add_curve_tube("serpentine_belt", belt_pts, 0.0125, MAT_RUBBER, explode=(-0.65, 0, 0), cyclic=True)

# ----------------------------------------------------------------------------
# rear plate + wiring harness + bolts
# ----------------------------------------------------------------------------
add_cyl("rear_plate", 0.19, 0.045, (0.345, 0, CRANK_Z + 0.025),
        rot=(0, radians(90), 0), mat=MAT_CAST, explode=(0.48, 0, 0.05))
add_cyl("rear_seal", 0.082, 0.03, (0.375, 0, CRANK_Z),
        rot=(0, radians(90), 0), mat=MAT_DGREY, explode=(0.48, 0, 0.05))

add_curve_tube("wiring_harness", [
    (-0.19, -0.02, 0.775),
    (-0.04, 0.03, 0.785),
    (0.13, -0.02, 0.775),
    (0.27, 0.04, 0.72),
], 0.010, MAT_RUBBER, explode=(0, 0, 0.58))

bi = 0
for side in (+1, -1):
    for bx in (-0.22, -0.08, 0.08, 0.22):
        for lat in (-0.08, 0.08):
            add_cyl(f"cover_bolt_{bi}", 0.010, 0.013,
                    on_bank(side, 0.378, x=bx, lat=lat),
                    rot=bank_rot(side), mat=MAT_CHROME,
                    explode=(0, side * 0.55, 0.42))
            bi += 1

# ----------------------------------------------------------------------------
# floor / lights / camera
# ----------------------------------------------------------------------------
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.32))
floor = bpy.context.active_object
floor.name = "studio_floor"
assign(floor, make_mat("floor_mat", (0.82, 0.82, 0.83), 0.0, 0.38))

world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next((n for n in world.node_tree.nodes if n.type == 'BACKGROUND'), None)
if bg is None:
    bg = world.node_tree.nodes.new('ShaderNodeBackground')
    out = world.node_tree.nodes.new('ShaderNodeOutputWorld')
    world.node_tree.links.new(bg.outputs['Background'], out.inputs['Surface'])
bg.inputs[0].default_value = (0.86, 0.86, 0.88, 1.0)
bg.inputs[1].default_value = 0.35

def add_light(name, loc, energy, size, rot):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.size = size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    return obj

add_light("key",  (-1.6, -1.7, 2.3), 320, 2.4, (radians(48), 0, radians(-42)))
add_light("fill", (1.8, -1.2, 1.5), 120, 2.2, (radians(62), 0, radians(55)))
add_light("rim",  (0.4, 2.0, 1.8), 180, 2.0, (radians(58), 0, radians(168)))
add_light("top",  (0, 0, 3.0), 90, 2.5, (0, 0, 0))

cam_data = bpy.data.cameras.new("Camera")
cam_data.lens = 58
cam = bpy.data.objects.new("Camera", cam_data)
scene.collection.objects.link(cam)
cam.location = (-1.72, -1.58, 1.32)
scene.camera = cam

bpy.ops.object.empty_add(location=(0, 0, 0.42))
aim = bpy.context.active_object
aim.name = "cam_target"
tc = cam.constraints.new('TRACK_TO')
tc.target = aim
tc.track_axis = 'TRACK_NEGATIVE_Z'
tc.up_axis = 'UP_Y'

# ----------------------------------------------------------------------------
# 拆装 animation: assembled -> exploded -> assembled, camera pulls back on explode
# ----------------------------------------------------------------------------
EXPLODE_SCALE = 0.7
for name, (obj, explode) in parts.items():
    if not any(explode):
        continue
    base = obj.location.copy()
    for frame, factor in ((1, 0.0), (60, 1.0), (120, 0.0)):
        obj.location = (base[0] + explode[0] * EXPLODE_SCALE * factor,
                        base[1] + explode[1] * EXPLODE_SCALE * factor,
                        base[2] + explode[2] * EXPLODE_SCALE * factor)
        obj.keyframe_insert(data_path="location", frame=frame)
    obj.location = base

cam_base = cam.location.copy()
cam_far = (cam_base[0] * 1.55, cam_base[1] * 1.55, cam_base[2] * 1.30)
for frame, loc in ((1, cam_base), (60, cam_far), (120, cam_base)):
    cam.location = loc
    cam.keyframe_insert(data_path="location", frame=frame)
cam.location = cam_base

scene.frame_set(1)
print("BUILD_OK parts=%d objects=%d" % (len(parts), len(bpy.data.objects)))
