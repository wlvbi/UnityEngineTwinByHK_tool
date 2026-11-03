# -*- coding: utf-8 -*-
"""V8 engine rebuilt to match the reference studio photo.
X = crank axis (front at -X), Y = lateral (camera side = -Y), Z = up.
"""
import bpy
import math
from math import radians, sin, cos, pi
from mathutils import Vector

# ---------------------------------------------------------------- reset
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
              bpy.data.lights, bpy.data.cameras, bpy.data.objects):
    for item in list(block):
        if item.users == 0:
            try:
                block.remove(item)
            except Exception:
                pass

scene = bpy.context.scene

# ---------------------------------------------------------------- materials
def make_mat(name, color, metallic=0.0, rough=0.5, bump=0.0, bump_scale=90.0, spec=None, coat=0.0):
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
    if coat > 0:
        if "Coat Weight" in bsdf.inputs:
            bsdf.inputs["Coat Weight"].default_value = coat
            bsdf.inputs["Coat Roughness"].default_value = 0.18
    if spec is not None and "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = spec
    if bump > 0:
        tex = nt.nodes.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = bump_scale
        tex.inputs['Detail'].default_value = 6.0
        tex.inputs['Roughness'].default_value = 0.65
        bumpn = nt.nodes.new('ShaderNodeBump')
        bumpn.inputs['Strength'].default_value = bump
        bumpn.inputs['Distance'].default_value = 0.004
        nt.links.new(tex.outputs['Fac'], bumpn.inputs['Height'])
        nt.links.new(bumpn.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

MAT_CAST    = make_mat("alu_cast",     (0.44, 0.45, 0.47), 1.0, 0.44, bump=0.28)
MAT_ALU     = make_mat("alu_machined", (0.80, 0.81, 0.83), 1.0, 0.27, bump=0.05, bump_scale=220)
MAT_POLISH  = make_mat("steel_polished", (0.85, 0.85, 0.87), 1.0, 0.12)
MAT_BLACK   = make_mat("black_plastic", (0.013, 0.013, 0.016), 0.0, 0.42, bump=0.05, bump_scale=260, spec=0.30, coat=0.35)
MAT_BLKMET  = make_mat("black_metal",  (0.025, 0.025, 0.029), 0.9, 0.42, spec=0.3)
MAT_CHROME  = make_mat("chrome",       (0.90, 0.90, 0.92), 1.0, 0.07)
MAT_GOLD    = make_mat("gold_brass",   (0.83, 0.62, 0.18), 1.0, 0.24)
MAT_BLUE    = make_mat("oil_filter_blue", (0.025, 0.13, 0.55), 0.35, 0.32)
MAT_YELLOW  = make_mat("dipstick_yellow", (0.90, 0.68, 0.02), 0.1, 0.34)
MAT_COPPER  = make_mat("copper",       (0.52, 0.23, 0.09), 1.0, 0.42)
MAT_RUBBER  = make_mat("rubber",       (0.012, 0.012, 0.012), 0.0, 0.80, bump=0.10, bump_scale=300)
MAT_STRIPE  = make_mat("stripe_silver", (0.80, 0.80, 0.82), 1.0, 0.16)
MAT_DGREY   = make_mat("dark_grey",    (0.075, 0.075, 0.082), 0.4, 0.45)
MAT_STEEL   = make_mat("steel_brushed", (0.40, 0.40, 0.42), 1.0, 0.36, bump=0.14, bump_scale=300)
MAT_RED     = make_mat("red_accent",   (0.45, 0.03, 0.03), 0.2, 0.35)

def cast_surface(obj, strength=0.0016, size=0.045):
    tex = bpy.data.textures.new(obj.name + "_cast", 'CLOUDS')
    tex.noise_scale = size
    tex.noise_depth = 2
    m = obj.modifiers.new("CastDisplace", 'DISPLACE')
    m.texture = tex
    m.strength = strength
    m.mid_level = 0.5
    return obj

def assign(obj, mat):
    if obj.data.materials:
        obj.data.materials.clear()
    obj.data.materials.append(mat)

def smooth(obj):
    if hasattr(obj.data, "polygons"):
        for p in obj.data.polygons:
            p.use_smooth = True
        obj.data.update()

def auto_smooth(obj, deg=32):
    try:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.shade_smooth_by_angle(angle=radians(deg))
    except Exception:
        pass

# ---------------------------------------------------------------- primitives
def _finish(obj, name, mat, bevel, seg=2):
    obj.name = name
    if bevel > 0:
        m = obj.modifiers.new("Bevel", 'BEVEL')
        m.width = bevel
        m.segments = seg
        m.limit_method = 'ANGLE'
    assign(obj, mat)
    return obj

def add_cube(name, size, loc, rot=(0, 0, 0), mat=MAT_CAST, bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return _finish(obj, name, mat, bevel)

def add_cyl(name, radius, depth, loc, rot=(0, 0, 0), mat=MAT_CAST,
            verts=40, bevel=0.003, smooth_it=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot)
    obj = bpy.context.active_object
    if smooth_it:
        smooth(obj)
    return _finish(obj, name, mat, bevel)

def add_cone(name, r1, r2, depth, loc, rot=(0, 0, 0), mat=MAT_ALU, verts=40):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                   depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    smooth(obj)
    return _finish(obj, name, mat, 0.002)

def add_torus(name, major, minor, loc, rot=(0, 0, 0), mat=MAT_BLACK, scale_z=1.0):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     location=loc, rotation=rot,
                                     major_segments=48, minor_segments=12)
    obj = bpy.context.active_object
    if scale_z != 1.0:
        obj.scale = (1, 1, scale_z)
        bpy.ops.object.transform_apply(scale=True)
    smooth(obj)
    return _finish(obj, name, mat, 0.0)

def add_tube(name, points, radius, mat, cyclic=False, res=16, bevel_res=8):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_res
    curve.resolution_u = res
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
    return obj

def add_bolt(name, loc, rot=(0, 0, 0), r=0.0095, h=0.012, mat=MAT_CHROME):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=r, depth=h,
                                        location=loc, rotation=rot)
    obj = bpy.context.active_object
    return _finish(obj, name, mat, 0.0015)

# ---------------------------------------------------------------- V layout helpers
BANK = radians(45.0)
S, C = sin(BANK), cos(BANK)
VALLEY_Z = 0.355
VALLEY_HALF = 0.10

def bank_rot(side):
    # local +Y -> bank normal, local +Z -> lateral
    return (radians(135.0) if side < 0 else radians(45.0), 0, 0)

def on_bank(side, d, x=0.0, lat=0.0):
    return (x,
            side * (VALLEY_HALF + S * d + C * lat),
            VALLEY_Z + C * d - S * lat)

def bank_cube(name, side, length, thick_n, width_t, d, x=0.0, lat=0.0,
              mat=MAT_CAST, bevel=0.01):
    """Box whose local Y is the bank normal and local Z the lateral axis."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=on_bank(side, d, x, lat),
                                    rotation=bank_rot(side))
    obj = bpy.context.active_object
    obj.scale = (length, thick_n, width_t)
    bpy.ops.object.transform_apply(scale=True)
    return _finish(obj, name, mat, bevel)

# ---------------------------------------------------------------- block
cast_surface(add_cube("engine_block", (0.545, 0.30, 0.25), (0, 0, 0.225), mat=MAT_CAST, bevel=0.012))
cast_surface(add_cube("block_valley", (0.50, 0.20, 0.055), (0, 0, VALLEY_Z - 0.02), mat=MAT_CAST, bevel=0.01))
add_cube("block_skirt_L", (0.50, 0.055, 0.10), (0, -0.155, 0.115), mat=MAT_CAST, bevel=0.012)
add_cube("block_skirt_R", (0.50, 0.055, 0.10), (0, 0.155, 0.115), mat=MAT_CAST, bevel=0.012)

# side ribs (cast look)
for i, rx in enumerate((-0.20, -0.10, 0.0, 0.10, 0.20)):
    for side in (-1, 1):
        add_cube(f"block_rib_{side}_{i}", (0.022, 0.012, 0.19),
                 (rx, side * 0.152, 0.21), mat=MAT_CAST, bevel=0.004)

# lifter valley cover + ribs
add_cube("lifter_valley", (0.46, 0.175, 0.02), (0, 0, VALLEY_Z + 0.012),
         mat=MAT_ALU, bevel=0.005)

# front timing cover
add_cube("timing_cover", (0.045, 0.315, 0.30), (-0.293, 0, 0.20),
         mat=MAT_ALU, bevel=0.02)
add_cyl("timing_cover_boss", 0.10, 0.03, (-0.32, 0, 0.20),
        rot=(0, radians(90), 0), mat=MAT_ALU)
# timing cover bolts
for i, (by, bz) in enumerate(((-0.125, 0.07), (0.125, 0.07), (-0.125, 0.33),
                              (0.125, 0.33), (0.0, 0.05), (0.0, 0.345))):
    add_bolt(f"timing_bolt_{i}", (-0.318, by, bz), rot=(0, radians(90), 0), r=0.009)

# rear flange / bellhousing
add_cyl("rear_plate", 0.185, 0.05, (0.30, 0, 0.21), rot=(0, radians(90), 0), mat=MAT_CAST)
add_cyl("rear_seal", 0.085, 0.03, (0.328, 0, 0.21), rot=(0, radians(90), 0), mat=MAT_DGREY)
for k in range(8):
    a = k * pi / 4
    add_bolt(f"rear_bolt_{k}", (0.325, 0.155 * cos(a), 0.21 + 0.155 * sin(a)),
             rot=(0, radians(90), 0), r=0.008)

# engine mounts
for side in (-1, 1):
    add_cube("mount_bracket_%d" % side, (0.10, 0.05, 0.10),
             (-0.02, side * 0.175, 0.145), mat=MAT_CAST, bevel=0.01)
    add_cyl("mount_rubber_%d" % side, 0.035, 0.045,
            (-0.02, side * 0.205, 0.115), rot=(radians(90), 0, 0), mat=MAT_RUBBER)

# ---------------------------------------------------------------- banks
COIL_X = (-0.20, -0.075, 0.05, 0.175)
PORT_X = (-0.20, -0.075, 0.05, 0.175)

for side, tag in ((+1, "R"), (-1, "L")):
    # head
    cast_surface(bank_cube(f"cylinder_head_{tag}", side, 0.55, 0.135, 0.195, 0.115,
              mat=MAT_CAST, bevel=0.012))
    # machined head rail (thin bright edge between head and cover)
    bank_cube(f"head_rail_{tag}", side, 0.55, 0.006, 0.170, 0.181,
              lat=0.005, mat=MAT_CAST, bevel=0.002)
    # head bolts along the outer edge
    for i, bx in enumerate((-0.235, -0.118, 0.0, 0.118, 0.235)):
        add_bolt(f"head_bolt_{tag}{i}", on_bank(side, 0.19, bx, 0.082),
                 rot=bank_rot(side), r=0.0085)
    # exhaust port flanges
    for j, bx in enumerate(PORT_X):
        bank_cube(f"exh_flange_{tag}{j}", side, 0.078, 0.024, 0.078, 0.140,
                  x=bx, lat=0.085, mat=MAT_ALU, bevel=0.004)

    # valve cover body
    bank_cube(f"valve_cover_{tag}", side, 0.555, 0.070, 0.190, 0.218,
              mat=MAT_BLACK, bevel=0.020)
    # raised top plateau
    bank_cube(f"cover_top_{tag}", side, 0.525, 0.014, 0.160, 0.260,
              mat=MAT_BLACK, bevel=0.010)

    # silver stripes (front half of the cover)
    for i, lat in enumerate((-0.078, -0.052, -0.026, 0.0)):
        bank_cube(f"stripe_{tag}{i}", side, 0.30, 0.004, 0.011, 0.270,
                  x=-0.13, lat=lat, mat=MAT_STRIPE, bevel=0.0015)

    # V8 badge at the rear of the cover
    bpy.ops.object.text_add(location=on_bank(side, 0.272, 0.172, 0.030))
    txt = bpy.context.active_object
    txt.data.body = "V8"
    txt.data.extrude = 0.004
    txt.data.size = 0.090
    txt.data.align_x = 'CENTER'
    txt.data.align_y = 'CENTER'
    txt.rotation_euler = (radians(45) if side < 0 else radians(-45), 0, 0)
    assign(txt, MAT_STRIPE)
    txt.name = f"badge_V8_{tag}"

    # oil filler cap
    add_cyl(f"oil_cap_{tag}", 0.030, 0.020, on_bank(side, 0.278, -0.245, 0.0),
            rot=bank_rot(side), mat=MAT_BLKMET)

    # ignition coils (4) sitting on the cover near the intake side
    for j, cx in enumerate(COIL_X):
        bank_cube(f"coil_{tag}{j}", side, 0.052, 0.050, 0.040, 0.292,
                  x=cx, lat=-0.058, mat=MAT_BLACK, bevel=0.011)
        bank_cube(f"coil_conn_{tag}{j}", side, 0.030, 0.022, 0.026, 0.322,
                  x=cx, lat=-0.062, mat=MAT_DGREY, bevel=0.005)
        add_cyl(f"coil_nub_{tag}{j}", 0.011, 0.026,
                on_bank(side, 0.310, cx, -0.058), rot=bank_rot(side), mat=MAT_DGREY)

    # cover perimeter bolts
    bi = 0
    for bx in (-0.255, -0.13, 0.0, 0.13, 0.255):
        for lat in (-0.093, 0.093):
            add_bolt(f"cover_bolt_{tag}{bi}", on_bank(side, 0.256, bx, lat),
                     rot=bank_rot(side), r=0.0075)
            bi += 1

    # fuel rail + injectors along the inboard top of the head
    add_cyl(f"fuel_rail_{tag}", 0.013, 0.47, on_bank(side, 0.190, 0.0, -0.080),
            rot=(0, radians(90), 0), mat=MAT_ALU)
    for j, cx in enumerate(COIL_X):
        add_cyl(f"injector_{tag}{j}", 0.011, 0.055,
                on_bank(side, 0.17, cx, -0.075), rot=bank_rot(side), mat=MAT_DGREY)

# cover gaskets (dark seam under the covers)
for side, tag in ((+1, "R"), (-1, "L")):
    bank_cube(f"cover_gasket_{tag}", side, 0.545, 0.012, 0.192, 0.181,
              mat=MAT_RUBBER, bevel=0.003)
    # spark plug wire from each coil down the inner cover edge
    for j, cx in enumerate(COIL_X):
        add_tube(f"coil_wire_{tag}{j}", [
            (cx, side * 0.175, 0.575), (cx + 0.008, side * 0.155, 0.55),
            (cx + 0.006, side * 0.150, 0.525)], 0.006, MAT_DGREY)

# ---------------------------------------------------------------- oil pan
add_cube("oil_pan_flange", (0.54, 0.335, 0.018), (0, 0, 0.10), mat=MAT_ALU, bevel=0.005)
cast_surface(add_cube("oil_pan", (0.50, 0.30, 0.10), (0, 0, 0.048), mat=MAT_ALU, bevel=0.012), 0.0010)
add_cube("oil_pan_sump", (0.30, 0.24, 0.05), (0.03, 0, 0.0), mat=MAT_ALU, bevel=0.012)
for i, ry in enumerate((-0.10, 0.0, 0.10)):
    add_cube(f"oil_pan_rib_{i}", (0.46, 0.014, 0.02), (0, ry, 0.012), mat=MAT_ALU, bevel=0.004)
for i, bx in enumerate((-0.22, -0.10, 0.02, 0.14, 0.245)):
    for by in (-0.155, 0.155):
        add_bolt(f"pan_bolt_{i}_{by}", (bx, by, 0.105), r=0.0072)
add_cyl("drain_plug", 0.018, 0.014, (0.10, 0.06, -0.026), rot=(0, 0, 0), mat=MAT_DGREY)

# block side bolt rows + casting ribs
for side in (-1, 1):
    for i, bx in enumerate((-0.22, -0.11, 0.0, 0.11, 0.22)):
        add_bolt(f"block_bolt_{side}_{i}", (bx, side * 0.153, 0.27),
                 rot=(0, radians(90), 0), r=0.0072)

# extra front face bolts and sensor bosses
for i, (by, bz) in enumerate(((-0.09, 0.13), (0.09, 0.13), (-0.09, 0.27), (0.09, 0.27))):
    add_bolt(f"timing_bolt2_{i}", (-0.317, by, bz), rot=(0, radians(90), 0), r=0.008)
add_cyl("oil_sensor", 0.016, 0.03, (-0.30, 0.10, 0.30), rot=(0, radians(90), 0), mat=MAT_DGREY)

# timing cover ribs
for i, ry in enumerate((-0.09, 0.0, 0.09)):
    add_cube(f"timing_rib_{i}", (0.018, 0.016, 0.22), (-0.318, ry, 0.18),
             mat=MAT_ALU, bevel=0.004)

# ---------------------------------------------------------------- intake
add_cube("intake_plenum", (0.50, 0.200, 0.105), (0.015, 0, 0.635),
         mat=MAT_BLACK, bevel=0.048)
add_cube("plenum_top", (0.40, 0.150, 0.018), (0.015, 0, 0.695),
         mat=MAT_BLACK, bevel=0.01)
# ribs across the plenum top
for i, rx in enumerate((-0.155, -0.095, -0.035, 0.025, 0.085, 0.145)):
    add_cube(f"plenum_rib_{i}", (0.010, 0.128, 0.006), (rx, 0, 0.706),
             mat=MAT_BLACK, bevel=0.005)
# plenum end caps
add_cyl("plenum_cap_f", 0.058, 0.03, (-0.225, 0, 0.635),
        rot=(0, radians(90), 0), mat=MAT_BLACK)
add_cyl("plenum_cap_r", 0.055, 0.03, (0.255, 0, 0.635),
        rot=(0, radians(90), 0), mat=MAT_BLACK)

# runners
for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate(PORT_X):
        pts = [
            (cx, side * 0.050, 0.675),
            (cx, side * 0.105, 0.658),
            (cx, side * 0.145, 0.616),
            (cx, side * 0.156, 0.578),
            (cx, side * 0.150, 0.552),
        ]
        add_tube(f"runner_{tag}{j}", pts, 0.022, MAT_BLACK)
        add_cyl(f"runner_flange_{tag}{j}", 0.037, 0.014,
                (cx, side * 0.172, 0.548), rot=(radians(90), 0, 0), mat=MAT_BLACK)

# throttle body (polished, gold butterfly)
TB_X, TB_Y, TB_Z = -0.268, -0.030, 0.635
add_cyl("throttle_body", 0.066, 0.105, (TB_X, TB_Y, TB_Z),
        rot=(0, radians(90), 0), mat=MAT_ALU)
add_cyl("throttle_bore", 0.056, 0.006, (TB_X - 0.054, TB_Y, TB_Z),
        rot=(0, radians(90), 0), mat=MAT_DGREY)
add_cyl("throttle_butterfly", 0.054, 0.007, (TB_X - 0.050, TB_Y, TB_Z),
        rot=(0, radians(90), radians(22)), mat=MAT_GOLD)
add_torus("throttle_lip", 0.064, 0.007, (TB_X - 0.056, TB_Y, TB_Z),
          rot=(0, radians(90), 0), mat=MAT_ALU)
add_cube("throttle_actuator", (0.06, 0.09, 0.07), (TB_X - 0.01, 0.092, TB_Z),
         mat=MAT_BLACK, bevel=0.012)
add_cyl("throttle_actuator_motor", 0.03, 0.03, (-0.27, 0.140, TB_Z),
        rot=(0, radians(90), 0), mat=MAT_DGREY)
# small sensors on the plenum
add_cyl("map_sensor", 0.016, 0.03, (0.145, -0.090, 0.672), rot=(radians(70), 0, 0), mat=MAT_DGREY)

# coolant crossover tubes across the front



# front thermostat housing + big coolant pipe (prominent in reference)
add_cyl("thermostat_housing", 0.058, 0.05, (-0.245, -0.115, 0.47),
        rot=(radians(15), radians(-20), 0), mat=MAT_ALU)
add_cyl("coolant_pipe", 0.038, 0.15, (-0.218, -0.180, 0.505),
        rot=(radians(-62), radians(-18), 0), mat=MAT_ALU)
add_cyl("coolant_bore", 0.031, 0.01, (-0.282, -0.257, 0.571),
        rot=(radians(-62), radians(-18), 0), mat=MAT_DGREY)
add_torus("coolant_pipe_lip", 0.039, 0.008, (-0.276, -0.252, 0.566),
          rot=(radians(-62), radians(-18), 0), mat=MAT_ALU)

# ---------------------------------------------------------------- exhaust headers
for side, tag in ((+1, "R"), (-1, "L")):
    for j, cx in enumerate(PORT_X):
        pts = [
            (cx, side * 0.258, 0.390),
            (cx, side * 0.322, 0.310),
            (cx + 0.015, side * 0.344, 0.215),
            (0.19 + j * 0.018, side * 0.344, 0.150),
            (0.255, side * 0.332, 0.125),
        ]
        add_tube(f"header_{tag}{j}", pts, 0.0205, MAT_STEEL)
        add_cyl(f"exh_port_{tag}{j}", 0.028, 0.02, (cx, side * 0.258, 0.390),
                rot=bank_rot(side), mat=MAT_DGREY)
    add_cyl(f"header_collector_{tag}", 0.041, 0.15, (0.335, side * 0.330, 0.115),
            rot=(0, radians(90), 0), mat=MAT_STEEL)
    add_cone(f"header_merge_{tag}", 0.056, 0.041, 0.075, (0.298, side * 0.332, 0.122),
             rot=(0, radians(-90), 0), mat=MAT_STEEL)
    add_cyl(f"header_flange_{tag}", 0.054, 0.014, (0.398, side * 0.330, 0.115),
            rot=(0, radians(90), 0), mat=MAT_ALU)
    for k in range(4):
        a = k * pi / 2 + pi / 4
        add_bolt(f"header_flange_bolt_{tag}{k}",
                 (0.406, side * 0.330 + 0.042 * cos(a), 0.115 + 0.042 * sin(a)),
                 rot=(0, radians(90), 0), r=0.006)
    add_tube(f"header_downpipe_{tag}", [
        (0.400, side * 0.330, 0.112), (0.432, side * 0.320, 0.072),
        (0.442, side * 0.302, 0.025)], 0.029, MAT_STEEL)


# ---------------------------------------------------------------- accessories (near side)
add_cyl("oil_filter", 0.043, 0.11, (-0.01, -0.255, 0.005),
        rot=(radians(115), 0, 0), mat=MAT_BLUE)
add_cyl("oil_filter_base", 0.034, 0.028, (-0.01, -0.235, 0.06),
        rot=(radians(115), 0, 0), mat=MAT_DGREY)

# dipstick (yellow loop near the cover)
add_cyl("dipstick_tube", 0.0065, 0.26, (-0.03, -0.235, 0.33),
        rot=(radians(28), radians(-8), 0), mat=MAT_DGREY)
add_torus("dipstick_handle", 0.028, 0.0055, (-0.005, -0.295, 0.455),
          rot=(radians(72), 0, radians(10)), mat=MAT_YELLOW)
add_cyl("dipstick_handle_stem", 0.005, 0.045, (-0.012, -0.283, 0.423),
        rot=(radians(70), 0, 0), mat=MAT_YELLOW)

# harness running along the far side
add_tube("wiring_harness", [(-0.24, 0.135, 0.60), (-0.05, 0.155, 0.62),
                            (0.15, 0.145, 0.60), (0.27, 0.11, 0.53)],
         0.011, MAT_RUBBER)
add_tube("pcv_hose", [(0.15, 0.12, 0.60), (0.21, 0.10, 0.66), (0.22, 0.05, 0.70)],
         0.010, MAT_RUBBER)

# ---------------------------------------------------------------- front accessory drive
PC = -0.365            # pulley center plane along X
BELT_X = -0.379        # belt plane
FRONT = -0.4085        # pulley front faces

def pulley(name, y, z, r, depth=0.055, mat=MAT_BLKMET, hub=True, grooves=0):
    add_cyl(name, r, depth, (PC, y, z), rot=(0, radians(90), 0), mat=mat)
    if grooves:
        for gi in range(grooves):
            add_torus(f"{name}_groove{gi}", r - 0.004, 0.0035,
                      (PC - depth / 2 + 0.004 + gi * 0.012, y, z),
                      rot=(0, radians(90), 0), mat=MAT_DGREY)
    if hub:
        add_cyl(f"{name}_hub", r * 0.30, depth + 0.03, (PC - 0.01, y, z),
                rot=(0, radians(90), 0), mat=MAT_CHROME)

pulley("crank_pulley", 0.0, 0.20, 0.105, 0.06, grooves=4)
pulley("waterpump_pulley", 0.0, 0.40, 0.072, 0.045)
pulley("alternator_pulley", 0.215, 0.495, 0.048, 0.035)
pulley("idler_pulley", -0.128, 0.300, 0.038, 0.035)
pulley("idler2_pulley", 0.112, 0.340, 0.036, 0.035)
pulley("idler3_pulley", -0.112, 0.180, 0.036, 0.035)
pulley("tensioner_pulley", 0.100, 0.445, 0.034, 0.035)
pulley("ac_pulley", -0.225, 0.145, 0.055, 0.04)

# shafts / bosses tying pulleys to the block face
add_cyl("crank_shaft", 0.028, 0.10, (-0.335, 0, 0.20), rot=(0, radians(90), 0), mat=MAT_CHROME)
add_cyl("waterpump_housing", 0.088, 0.075, (-0.315, 0, 0.40),
        rot=(0, radians(90), 0), mat=MAT_ALU)
add_cyl("waterpump_neck", 0.05, 0.06, (-0.355, 0, 0.40), rot=(0, radians(90), 0), mat=MAT_ALU)

# alternator: silver caps + copper core
ALT_Y, ALT_Z = 0.215, 0.495
add_cyl("alternator_rear", 0.079, 0.05, (-0.30, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_ALU)
add_cyl("alternator_core", 0.077, 0.075, (-0.34, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_COPPER)
add_cyl("alternator_front", 0.058, 0.035, (-0.352, ALT_Y, ALT_Z), rot=(0, radians(90), 0), mat=MAT_ALU)
for k in range(8):
    a = k * pi / 4
    add_cube(f"alt_vent_{k}", (0.008, 0.030, 0.012),
             (-0.383, ALT_Y + 0.066 * cos(a), ALT_Z + 0.066 * sin(a)),
             mat=MAT_DGREY, bevel=0.002)
add_cube("alt_connector", (0.045, 0.05, 0.04), (-0.312, ALT_Y + 0.055, ALT_Z + 0.06),
         mat=MAT_DGREY, bevel=0.006)
add_cube("alternator_bracket", (0.05, 0.16, 0.10), (-0.325, 0.16, 0.50),
         mat=MAT_CAST, bevel=0.012)

# AC compressor (far side, low front)
add_cyl("ac_compressor", 0.072, 0.13, (-0.335, -0.225, 0.145), rot=(0, radians(90), 0), mat=MAT_BLKMET)
add_cyl("ac_clutch", 0.056, 0.025, (-0.374, -0.225, 0.145), rot=(0, radians(90), 0), mat=MAT_ALU)
add_cyl("ac_hose_port", 0.02, 0.06, (-0.31, -0.20, 0.225), rot=(radians(25), 0, radians(-15)), mat=MAT_ALU)

# tensioner arm
add_cube("tensioner_arm", (0.05, 0.13, 0.035), (-0.36, 0.09, 0.35),
         rot=(radians(-35), 0, 0), mat=MAT_CAST, bevel=0.008)

# ---------------------------------------------------------------- serpentine belt (hull of pulleys)
def convex_hull(points):
    pts = sorted(set(points))
    if len(pts) <= 2:
        return pts
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower = []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]

pulley_circles = [(0.0, 0.20, 0.105), (0.0, 0.40, 0.072), (0.215, 0.495, 0.048),
                  (-0.128, 0.300, 0.038), (0.100, 0.445, 0.034),
                  (0.112, 0.340, 0.036), (-0.112, 0.180, 0.036), (-0.225, 0.145, 0.055)]
outer, inner = [], []
for (py, pz, pr) in pulley_circles:
    ro, ri = pr + 0.0035, pr - 0.0045
    for k in range(144):
        a = 2 * pi * k / 144
        outer.append((round(py + ro * cos(a), 6), round(pz + ro * sin(a), 6)))
        inner.append((round(py + ri * cos(a), 6), round(pz + ri * sin(a), 6)))
pairs = dict(zip(outer, inner))
hull = convex_hull(outer)
ring_in = [pairs[p] for p in hull]

belt_mesh = bpy.data.meshes.new("belt_mesh")
n = len(hull)
verts = [(BELT_X, y, z) for (y, z) in hull] + [(BELT_X, y, z) for (y, z) in ring_in]
faces = [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
belt_mesh.from_pydata(verts, [], faces)
belt_mesh.update()
belt = bpy.data.objects.new("serpentine_belt", belt_mesh)
scene.collection.objects.link(belt)
assign(belt, MAT_RUBBER)
sol = belt.modifiers.new("Solidify", 'SOLIDIFY')
sol.thickness = 0.020
sol.offset = 0.0

# ---------------------------------------------------------------- studio
bpy.ops.mesh.primitive_plane_add(size=250, location=(0, 0, -0.075))
floor = bpy.context.active_object
floor.name = "studio_floor"
assign(floor, make_mat("floor_mat", (0.78, 0.78, 0.79), 0.0, 0.42))

world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next((n for n in world.node_tree.nodes if n.type == 'BACKGROUND'), None)
if bg is None:
    bg = world.node_tree.nodes.new('ShaderNodeBackground')
    out = world.node_tree.nodes.new('ShaderNodeOutputWorld')
    world.node_tree.links.new(bg.outputs['Background'], out.inputs['Surface'])
bg.inputs[0].default_value = (0.83, 0.84, 0.86, 1.0)
bg.inputs[1].default_value = 0.44

# aim target for lights and camera
bpy.ops.object.empty_add(location=(-0.01, 0, 0.34))
aim = bpy.context.active_object
aim.name = "cam_target"

def add_area(name, loc, energy, size, color=(1, 1, 1)):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.shape = 'RECTANGLE'
    data.size = size
    data.size_y = size * 0.6
    data.color = color
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = loc
    con = obj.constraints.new('TRACK_TO')
    con.target = aim
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    return obj

add_area("key", (-1.95, -1.7, 2.15), 420, 2.6)
add_area("fill", (1.9, -1.15, 1.35), 70, 3.0, (0.96, 0.97, 1.0))
add_area("rim", (0.35, 2.2, 1.75), 240, 2.2, (1.0, 0.99, 0.97))
add_area("top", (0.0, 0.35, 3.1), 70, 3.2)
add_area("front_low", (-1.55, -0.45, 0.30), 35, 1.6)

cam_data = bpy.data.cameras.new("Camera")
cam_data.lens = 62
cam = bpy.data.objects.new("Camera", cam_data)
scene.collection.objects.link(cam)
cam.location = (-1.50, -1.61, 0.82)
tc = cam.constraints.new('TRACK_TO')
tc.target = aim
tc.track_axis = 'TRACK_NEGATIVE_Z'
tc.up_axis = 'UP_Y'
scene.camera = cam

# ---------------------------------------------------------------- render settings
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.taa_render_samples = 128
try:
    scene.eevee.use_raytracing = True
    scene.eevee.ray_tracing_method = 'SCREEN'
except Exception as e:
    print("raytrace cfg:", e)
try:
    scene.eevee.use_fast_gi = True
    scene.eevee.fast_gi_distance = 0.35
    scene.eevee.fast_gi_quality = 0.5
except Exception as e:
    print("fastgi cfg:", e)
try:
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'GPU'
    scene.cycles.samples = 192
    scene.cycles.use_denoising = True
except Exception as e:
    print('cycles cfg:', e)
scene.render.resolution_x = 1600
scene.render.resolution_y = 1200
scene.render.film_transparent = False
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -0.18

print("BUILD_OK objects=%d hull=%d" % (len(bpy.data.objects), len(hull)))
