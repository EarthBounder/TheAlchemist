"""Generate ComfyUI workflow JSON: 8-dir img2img + OpenPose ControlNet + IPAdapter + Pixel8Bit."""
import json
from collections import defaultdict

DIRS = [
    ("N", "north, back view only, facing away from camera"),
    ("NE", "northeast diagonal, 3/4 back view only"),
    ("E", "east, strict right side profile only, one eye visible"),
    ("SE", "southeast diagonal, 3/4 front view only"),
    ("S", "south, front view only, facing camera"),
    ("SW", "southwest diagonal, 3/4 front view only"),
    ("W", "west, strict left side profile only, one eye visible"),
    ("NW", "northwest diagonal, 3/4 back view only"),
]

NEG = (
    "different character, wrong face, face drift, asymmetrical face swap, low quality, blurry, deformed, "
    "watermark, text, signature, depth of field, photoreal, smooth shading, gradients, anti-aliased edges, "
    "noisy texture, extra colors outside palette, wrong facing direction, wrong pose"
)

POSE_PREFIX = "poses/openpose_"
HERO = "hero_reference.png"

CKPT = "sd_xl_base_1.0.safetensors"
LORA = "pixel-art-xl-v1.1.safetensors"
CONTROLNET = "OpenPoseXL2.safetensors"
IPADAPTER_MODEL = "ip-adapter-plus-face_sdxl_vit-h.safetensors"
CLIP_VISION = "CLIP-ViT-bigG-14-laion2B-39B-b160k.safetensors"

N_CKPT = 4
N_LORA = 39
N_NEG = 7
N_HERO_IMG = 48
N_HERO_SCALE = 49
N_VAE_ENC = 51
N_IPA_LOAD = 15
N_CLIP_V = 16
N_PREP = 17
N_IPA = 14
N_CN_LOAD = 203


def pos_prompt(code: str, facing: str) -> str:
    return (
        f"same hero character as reference image, preserve face and identity, full body, walk cycle frame A, "
        f"facing {facing}, ONLY this orientation, match openpose skeleton exactly, "
        f"Chrono Trigger inspired 16-bit JRPG sprite, clean pixel clusters, crisp silhouette, "
        f"no anti-aliasing, simple flat shading, readable at 36x36, direction {code}"
    )


def attach_output_links(nodes: list, links: list) -> None:
    by_id = {n["id"]: n for n in nodes}
    out_map: dict[int, dict[int, list]] = defaultdict(lambda: defaultdict(list))
    for lid, src, src_slot, _, _, _ in links:
        out_map[src][src_slot].append(lid)
    for nid, n in by_id.items():
        outs = n.get("outputs")
        if not outs:
            continue
        for o in outs:
            slot = o.get("slot_index", 0)
            o["links"] = out_map[nid].get(slot, [])


def main() -> None:
    nodes: list = []
    links: list = []
    link_id = 1

    def L(src_id, src_slot, dst_id, dst_slot, typ: str) -> int:
        nonlocal link_id
        links.append([link_id, src_id, src_slot, dst_id, dst_slot, typ])
        link_id += 1
        return link_id - 1

    nodes.append(
        {
            "id": N_CKPT,
            "type": "CheckpointLoaderSimple",
            "pos": [-820, 180],
            "size": [315, 98],
            "flags": {},
            "order": 0,
            "mode": 0,
            "outputs": [
                {"name": "MODEL", "type": "MODEL", "links": [], "slot_index": 0},
                {"name": "CLIP", "type": "CLIP", "links": [], "slot_index": 1},
                {"name": "VAE", "type": "VAE", "links": [], "slot_index": 2},
            ],
            "title": "Load Base Safetensors",
            "properties": {"Node name for S&R": "CheckpointLoaderSimple"},
            "widgets_values": [CKPT],
        }
    )
    nodes.append(
        {
            "id": N_LORA,
            "type": "LoraLoader",
            "pos": [-460, 180],
            "size": [315, 126],
            "flags": {},
            "order": 1,
            "mode": 0,
            "inputs": [
                {"name": "model", "type": "MODEL", "link": L(N_CKPT, 0, N_LORA, 0, "MODEL")},
                {"name": "clip", "type": "CLIP", "link": L(N_CKPT, 1, N_LORA, 1, "CLIP")},
            ],
            "outputs": [
                {"name": "MODEL", "type": "MODEL", "links": [], "slot_index": 0},
                {"name": "CLIP", "type": "CLIP", "links": [], "slot_index": 1},
            ],
            "properties": {"Node name for S&R": "LoraLoader"},
            "widgets_values": [LORA, 1.0, 1.0],
        }
    )

    nodes.append(
        {
            "id": N_NEG,
            "type": "CLIPTextEncode",
            "pos": [-460, 360],
            "size": [520, 200],
            "flags": {},
            "order": 2,
            "mode": 0,
            "inputs": [{"name": "clip", "type": "CLIP", "link": L(N_LORA, 1, N_NEG, 0, "CLIP")}],
            "outputs": [{"name": "CONDITIONING", "type": "CONDITIONING", "links": [], "slot_index": 0}],
            "title": "Prompt Negative (shared)",
            "properties": {"Node name for S&R": "CLIPTextEncode"},
            "widgets_values": [NEG],
        }
    )

    nodes.append(
        {
            "id": N_HERO_IMG,
            "type": "LoadImage",
            "pos": [-820, 340],
            "size": [315, 314],
            "flags": {},
            "order": 3,
            "mode": 0,
            "outputs": [
                {"name": "IMAGE", "type": "IMAGE", "links": [], "shape": 3, "slot_index": 0},
                {"name": "MASK", "type": "MASK", "links": None, "shape": 3, "slot_index": 1},
            ],
            "title": "Hero reference (identity + img2img)",
            "properties": {"Node name for S&R": "LoadImage"},
            "widgets_values": [HERO, "image"],
        }
    )
    nodes.append(
        {
            "id": N_HERO_SCALE,
            "type": "ImageScale",
            "pos": [-460, 420],
            "size": [315, 130],
            "flags": {},
            "order": 4,
            "mode": 0,
            "inputs": [{"name": "image", "type": "IMAGE", "link": L(N_HERO_IMG, 0, N_HERO_SCALE, 0, "IMAGE")}],
            "outputs": [{"name": "IMAGE", "type": "IMAGE", "links": [], "slot_index": 0}],
            "title": "Hero 1024 (match latent)",
            "properties": {"Node name for S&R": "ImageScale"},
            "widgets_values": ["nearest-exact", 1024, 1024, "disabled"],
        }
    )
    nodes.append(
        {
            "id": N_VAE_ENC,
            "type": "VAEEncode",
            "pos": [-100, 420],
            "size": [210, 46],
            "flags": {},
            "order": 5,
            "mode": 0,
            "inputs": [
                {"name": "pixels", "type": "IMAGE", "link": L(N_HERO_SCALE, 0, N_VAE_ENC, 0, "IMAGE")},
                {"name": "vae", "type": "VAE", "link": L(N_CKPT, 2, N_VAE_ENC, 1, "VAE")},
            ],
            "outputs": [{"name": "LATENT", "type": "LATENT", "links": [], "slot_index": 0}],
            "properties": {"Node name for S&R": "VAEEncode"},
        }
    )

    nodes.append(
        {
            "id": N_IPA_LOAD,
            "type": "IPAdapterModelLoader",
            "pos": [-820, -120],
            "size": [315, 58],
            "flags": {},
            "order": 6,
            "mode": 0,
            "outputs": [{"name": "IPADAPTER", "type": "IPADAPTER", "links": [], "shape": 3}],
            "title": "IP-Adapter model (SDXL)",
            "properties": {"Node name for S&R": "IPAdapterModelLoader"},
            "widgets_values": [IPADAPTER_MODEL],
        }
    )
    nodes.append(
        {
            "id": N_CLIP_V,
            "type": "CLIPVisionLoader",
            "pos": [-820, -30],
            "size": [315, 58],
            "flags": {},
            "order": 7,
            "mode": 0,
            "outputs": [{"name": "CLIP_VISION", "type": "CLIP_VISION", "links": [], "shape": 3}],
            "title": "CLIP Vision (match IP-Adapter)",
            "properties": {"Node name for S&R": "CLIPVisionLoader"},
            "widgets_values": [CLIP_VISION],
        }
    )
    nodes.append(
        {
            "id": N_PREP,
            "type": "PrepImageForClipVision",
            "pos": [-460, -80],
            "size": [315, 106],
            "flags": {},
            "order": 8,
            "mode": 0,
            "inputs": [{"name": "image", "type": "IMAGE", "link": L(N_HERO_SCALE, 0, N_PREP, 0, "IMAGE")}],
            "outputs": [{"name": "IMAGE", "type": "IMAGE", "links": [], "shape": 3, "slot_index": 0}],
            "title": "Prep hero for IP-Adapter",
            "properties": {"Node name for S&R": "PrepImageForClipVision"},
            "widgets_values": ["LANCZOS", "center", 0.12],
        }
    )
    nodes.append(
        {
            "id": N_IPA,
            "type": "IPAdapterAdvanced",
            "pos": [-100, -60],
            "size": [315, 278],
            "flags": {},
            "order": 9,
            "mode": 0,
            "inputs": [
                {"name": "model", "type": "MODEL", "link": L(N_LORA, 0, N_IPA, 0, "MODEL")},
                {"name": "ipadapter", "type": "IPADAPTER", "link": L(N_IPA_LOAD, 0, N_IPA, 1, "IPADAPTER")},
                {"name": "image", "type": "IMAGE", "link": L(N_PREP, 0, N_IPA, 2, "IMAGE")},
                {"name": "image_negative", "type": "IMAGE", "link": None},
                {"name": "attn_mask", "type": "MASK", "link": None},
                {"name": "clip_vision", "type": "CLIP_VISION", "link": L(N_CLIP_V, 0, N_IPA, 5, "CLIP_VISION")},
            ],
            "outputs": [{"name": "MODEL", "type": "MODEL", "links": [], "shape": 3, "slot_index": 0}],
            "title": "IP-Adapter (lock identity)",
            "properties": {"Node name for S&R": "IPAdapterAdvanced"},
            "widgets_values": [0.72, "linear", "concat", 0.0, 1.0, "K+V"],
        }
    )

    nodes.append(
        {
            "id": N_CN_LOAD,
            "type": "ControlNetLoader",
            "pos": [200, -140],
            "size": [315, 58],
            "flags": {},
            "order": 10,
            "mode": 0,
            "outputs": [{"name": "CONTROL_NET", "type": "CONTROL_NET", "links": [], "shape": 3}],
            "title": "ControlNet OpenPose XL",
            "properties": {"Node name for S&R": "ControlNetLoader"},
            "widgets_values": [CONTROLNET],
        }
    )

    base_x = 200
    y0 = 120
    dy = 310

    for i, (code, facing) in enumerate(DIRS):
        y = y0 + i * dy
        pid = 300 + i
        cid = 400 + i
        aid = 500 + i
        kid = 600 + i
        did = 700 + i
        qid = 800 + i
        sid = 900 + i

        pose_file = f"{POSE_PREFIX}{code}.png"

        nodes.append(
            {
                "id": pid,
                "type": "LoadImage",
                "pos": [base_x - 200, y],
                "size": [315, 314],
                "flags": {},
                "order": 20 + i,
                "mode": 0,
                "outputs": [
                    {"name": "IMAGE", "type": "IMAGE", "links": [], "shape": 3, "slot_index": 0},
                    {"name": "MASK", "type": "MASK", "links": None, "shape": 3, "slot_index": 1},
                ],
                "title": f"Pose {code} (OpenPose preview)",
                "properties": {"Node name for S&R": "LoadImage"},
                "widgets_values": [pose_file, "image"],
            }
        )
        nodes.append(
            {
                "id": cid,
                "type": "CLIPTextEncode",
                "pos": [base_x + 140, y],
                "size": [480, 140],
                "flags": {},
                "order": 30 + i,
                "mode": 0,
                "inputs": [{"name": "clip", "type": "CLIP", "link": L(N_LORA, 1, cid, 0, "CLIP")}],
                "outputs": [{"name": "CONDITIONING", "type": "CONDITIONING", "links": [], "slot_index": 0}],
                "title": f"Prompt + {code}",
                "properties": {"Node name for S&R": "CLIPTextEncode"},
                "widgets_values": [pos_prompt(code, facing)],
            }
        )
        nodes.append(
            {
                "id": aid,
                "type": "ControlNetApplyAdvanced",
                "pos": [base_x + 680, y],
                "size": [315, 186],
                "flags": {},
                "order": 40 + i,
                "mode": 0,
                "inputs": [
                    {"name": "positive", "type": "CONDITIONING", "link": L(cid, 0, aid, 0, "CONDITIONING")},
                    {"name": "negative", "type": "CONDITIONING", "link": L(N_NEG, 0, aid, 1, "CONDITIONING")},
                    {"name": "control_net", "type": "CONTROL_NET", "link": L(N_CN_LOAD, 0, aid, 2, "CONTROL_NET")},
                    {"name": "image", "type": "IMAGE", "link": L(pid, 0, aid, 3, "IMAGE")},
                ],
                "outputs": [
                    {"name": "positive", "type": "CONDITIONING", "links": [], "slot_index": 0},
                    {"name": "negative", "type": "CONDITIONING", "links": [], "slot_index": 1},
                ],
                "title": f"OpenPose -> {code}",
                "properties": {"Node name for S&R": "ControlNetApplyAdvanced"},
                "widgets_values": [0.88, 0.0, 1.0],
            }
        )
        nodes.append(
            {
                "id": kid,
                "type": "KSampler",
                "pos": [base_x + 1040, y],
                "size": [315, 262],
                "flags": {},
                "order": 50 + i,
                "mode": 0,
                "inputs": [
                    {"name": "model", "type": "MODEL", "link": L(N_IPA, 0, kid, 0, "MODEL")},
                    {"name": "positive", "type": "CONDITIONING", "link": L(aid, 0, kid, 1, "CONDITIONING")},
                    {"name": "negative", "type": "CONDITIONING", "link": L(aid, 1, kid, 2, "CONDITIONING")},
                    {"name": "latent_image", "type": "LATENT", "link": L(N_VAE_ENC, 0, kid, 3, "LATENT")},
                ],
                "outputs": [{"name": "LATENT", "type": "LATENT", "links": [], "slot_index": 0}],
                "title": f"KSampler {code}",
                "properties": {"Node name for S&R": "KSampler"},
                "widgets_values": [1073741824, "randomize", 24, 7.0, "euler_ancestral", "karras", 0.52],
            }
        )
        nodes.append(
            {
                "id": did,
                "type": "VAEDecode",
                "pos": [base_x + 1400, y + 40],
                "size": [210, 46],
                "flags": {},
                "order": 60 + i,
                "mode": 0,
                "inputs": [
                    {"name": "samples", "type": "LATENT", "link": L(kid, 0, did, 0, "LATENT")},
                    {"name": "vae", "type": "VAE", "link": L(N_CKPT, 2, did, 1, "VAE")},
                ],
                "outputs": [{"name": "IMAGE", "type": "IMAGE", "links": [], "slot_index": 0}],
                "properties": {"Node name for S&R": "VAEDecode"},
            }
        )
        nodes.append(
            {
                "id": qid,
                "type": "Pixel8Bit",
                "pos": [base_x + 1660, y + 20],
                "size": [360, 260],
                "flags": {},
                "order": 70 + i,
                "mode": 0,
                "inputs": [{"name": "image", "type": "IMAGE", "link": L(did, 0, qid, 0, "IMAGE")}],
                "outputs": [{"name": "image", "type": "IMAGE", "links": [], "slot_index": 0}],
                "title": f"Palette {code}",
                "properties": {"Node name for S&R": "Pixel8Bit"},
                "widgets_values": [
                    1,
                    "Fixed Palette",
                    "Custom",
                    "#f6cd26,#ac6b26,#563226,#331c17,#bb7f57,#725956,#393939,#202020",
                    8,
                    "None",
                    4,
                    8,
                    1,
                ],
            }
        )
        nodes.append(
            {
                "id": sid,
                "type": "SaveImage",
                "pos": [base_x + 2060, y + 20],
                "size": [315, 270],
                "flags": {},
                "order": 80 + i,
                "mode": 0,
                "inputs": [{"name": "images", "type": "IMAGE", "link": L(qid, 0, sid, 0, "IMAGE")}],
                "properties": {},
                "widgets_values": [f"hero_pose_ipa_walkA_{code}_"],
            }
        )

    attach_output_links(nodes, links)

    last_node_id = max(n["id"] for n in nodes)
    out = {
        "last_node_id": last_node_id,
        "last_link_id": link_id - 1,
        "nodes": nodes,
        "links": links,
        "groups": [],
        "config": {},
        "extra": {},
        "version": 0.4,
    }

    path = r"c:\TheAlchemist\art_gen\hero-8dir-walkA-pose-ipadapter-rust-gold-8-queued.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
    print("Wrote", path, "nodes", len(nodes), "links", len(links))


if __name__ == "__main__":
    main()
