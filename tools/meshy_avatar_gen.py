"""
NEREON Meshy.ai Avatar Generator
Generates Ignis / Aquilo / Terron / Zephos GLB models via Meshy Text-to-3D API.

Workflow per avatar:
  1. POST /openapi/v2/text-to-3d  (mode=preview, a-pose)
  2. Poll until SUCCEEDED
  3. POST /openapi/v2/text-to-3d  (mode=refine, enable_pbr=True)
  4. Poll until SUCCEEDED
  5. Download .glb → Assets/_NEREON/Models/Avatars/<Name>/

Usage:
  python tools/meshy_avatar_gen.py
  python tools/meshy_avatar_gen.py --avatar ignis        # single avatar
  python tools/meshy_avatar_gen.py --stage preview-only  # skip refine/download

Requires: pip install requests python-dotenv
API docs: https://docs.meshy.ai/api-text-to-3d
"""

import os
import sys
import time
import json
import argparse
import requests
from pathlib import Path

# ── Config ────────────────────────────────────────────────────────────────────

SCRIPT_DIR   = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
DOTENV_PATH  = PROJECT_ROOT / ".env"
OUTPUT_BASE  = PROJECT_ROOT / "unity/NEREON/NEREON/Assets/_NEREON/Models/Avatars"
MESHY_BASE   = "https://api.meshy.ai"

POLL_INTERVAL = 8   # seconds between status checks
MAX_WAIT      = 600 # seconds (10 min) before timeout per stage

# ── Avatar definitions ────────────────────────────────────────────────────────

AVATARS = {
    "ignis": {
        "name": "Ignis",
        "element": "Fire",
        "prompt": (
            "Cute chubby small fire demon monster, round molten lava body with cracked obsidian skin, "
            "tiny flickering flame horns, large expressive amber eyes, short stubby arms and legs, "
            "low-poly stylized game character, hand-painted PBR textures, toon shading, "
            "Sleepagotchi aesthetic, humanoid biped, A-pose, full body visible, no background"
        ),
        "negative_prompt": (
            "realistic, photorealistic, multiple characters, weapons, base, pedestal, "
            "floating parts, T-pose, extra limbs"
        ),
    },
    "aquilo": {
        "name": "Aquilo",
        "element": "Water",
        "prompt": (
            "Cute chubby small water demon monster, translucent blue jelly-like body with floating bubbles inside, "
            "small wave crest on top of head, large deep-sea teal eyes, small fin ears, "
            "low-poly stylized game character, stylized PBR textures, vibrant blue color palette, "
            "Sleepagotchi aesthetic, humanoid biped, A-pose, full body visible, no background"
        ),
        "negative_prompt": (
            "realistic, photorealistic, multiple characters, weapons, base, pedestal, "
            "floating parts, T-pose, extra limbs"
        ),
    },
    "terron": {
        "name": "Terron",
        "element": "Earth",
        "prompt": (
            "Cute chubby small earth golem monster, round body made of moss-covered stone and pebbles, "
            "tiny sprout with two leaves growing from head, large earthy brown eyes, "
            "sturdy stone-like hands and feet, friendly golem appearance, "
            "low-poly stylized game character, mossy rock PBR textures, soft organic lighting, "
            "Sleepagotchi aesthetic, humanoid biped, A-pose, full body visible, no background"
        ),
        "negative_prompt": (
            "realistic, photorealistic, multiple characters, weapons, base, pedestal, "
            "floating parts, T-pose, extra limbs"
        ),
    },
    "zephos": {
        "name": "Zephos",
        "element": "Air",
        "prompt": (
            "Cute chubby small air spirit monster, fluffy white cloud-like body with semi-transparent vaporous wisps, "
            "tiny golden wings on sides of head, large bright sky-blue eyes, "
            "light airy silhouette with floating energy particles, "
            "low-poly stylized game character, soft cloud PBR textures, ethereal glow, "
            "Sleepagotchi aesthetic, humanoid biped, A-pose, full body visible, no background"
        ),
        "negative_prompt": (
            "realistic, photorealistic, multiple characters, weapons, base, pedestal, "
            "floating parts, T-pose, extra limbs"
        ),
    },
}

# ── Helpers ───────────────────────────────────────────────────────────────────

def load_api_key() -> str:
    """Read MESHY_API_KEY from .env or environment."""
    key = os.environ.get("MESHY_API_KEY", "")
    if key and key != "msy-your-key-here":
        return key

    if DOTENV_PATH.exists():
        for line in DOTENV_PATH.read_text().splitlines():
            line = line.strip()
            if line.startswith("MESHY_API_KEY="):
                key = line.split("=", 1)[1].strip().strip('"').strip("'")
                if key and key != "msy-your-key-here":
                    return key

    print("ERROR: MESHY_API_KEY not set.")
    print(f"  Create {DOTENV_PATH} with:  MESHY_API_KEY=msy-<your-key>")
    sys.exit(1)


def auth_headers(api_key: str) -> dict:
    return {"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"}


def post_preview(api_key: str, avatar: dict) -> str:
    """Submit a text-to-3D preview task. Returns task_id."""
    payload = {
        "mode": "preview",
        "prompt": avatar["prompt"],
        "negative_prompt": avatar["negative_prompt"],
        "art_style": "cartoon",
        "pose_mode": "a-pose",
        "model": "meshy-6",
        "seed": 42,
    }
    resp = requests.post(
        f"{MESHY_BASE}/openapi/v2/text-to-3d",
        headers=auth_headers(api_key),
        json=payload,
        timeout=30,
    )
    resp.raise_for_status()
    data = resp.json()
    task_id = data.get("result") or data.get("id")
    print(f"  Preview submitted → task_id: {task_id}")
    return task_id


def post_refine(api_key: str, preview_task_id: str) -> str:
    """Submit a refine task from a completed preview. Returns task_id."""
    payload = {
        "mode": "refine",
        "preview_task_id": preview_task_id,
        "enable_pbr": True,
        "texture_richness": "high",
    }
    resp = requests.post(
        f"{MESHY_BASE}/openapi/v2/text-to-3d",
        headers=auth_headers(api_key),
        json=payload,
        timeout=30,
    )
    resp.raise_for_status()
    data = resp.json()
    task_id = data.get("result") or data.get("id")
    print(f"  Refine  submitted → task_id: {task_id}")
    return task_id


def poll_task(api_key: str, task_id: str, label: str) -> dict:
    """Poll until SUCCEEDED or FAILED. Returns final task data."""
    deadline = time.time() + MAX_WAIT
    dots = 0
    while time.time() < deadline:
        resp = requests.get(
            f"{MESHY_BASE}/openapi/v2/text-to-3d/{task_id}",
            headers=auth_headers(api_key),
            timeout=15,
        )
        resp.raise_for_status()
        data = resp.json()
        status = data.get("status", "UNKNOWN")
        progress = data.get("progress", 0)

        if status == "SUCCEEDED":
            print(f"\r  {label}: SUCCEEDED ({progress}%)          ")
            return data
        elif status in ("FAILED", "EXPIRED"):
            print(f"\r  {label}: {status}                          ")
            print(f"  Error: {data.get('task_error', {}).get('message', 'unknown')}")
            raise RuntimeError(f"Task {task_id} {status}")
        else:
            dots = (dots + 1) % 4
            print(f"\r  {label}: {status} {progress}%{'.'*dots}   ", end="", flush=True)
            time.sleep(POLL_INTERVAL)

    raise TimeoutError(f"Task {task_id} timed out after {MAX_WAIT}s")


def download_file(url: str, out_path: Path, label: str):
    """Download a file with size reporting."""
    print(f"  Downloading {label} → {out_path.name} ...", end=" ", flush=True)
    resp = requests.get(url, timeout=120, stream=True)
    resp.raise_for_status()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "wb") as f:
        for chunk in resp.iter_content(chunk_size=8192):
            f.write(chunk)
    size_mb = out_path.stat().st_size / (1024 * 1024)
    print(f"done ({size_mb:.1f} MB)")


def save_task_json(avatar_dir: Path, stage: str, data: dict):
    (avatar_dir / f"task_{stage}.json").write_text(json.dumps(data, indent=2))


# ── Per-avatar pipeline ───────────────────────────────────────────────────────

def process_avatar(api_key: str, avatar: dict, stage: str):
    avatar_dir = OUTPUT_BASE / avatar["name"]
    avatar_dir.mkdir(parents=True, exist_ok=True)

    print(f"\n{'='*60}")
    print(f"  {avatar['element']}  —  {avatar['name']}")
    print(f"{'='*60}")

    # Stage 1 — Preview
    preview_cache = avatar_dir / "task_preview.json"
    if preview_cache.exists():
        prev_data = json.loads(preview_cache.read_text())
        preview_task_id = prev_data["id"]
        print(f"  Preview cached: {preview_task_id}  status={prev_data.get('status')}")
        if prev_data.get("status") != "SUCCEEDED":
            prev_data = poll_task(api_key, preview_task_id, "Preview")
            save_task_json(avatar_dir, "preview", prev_data)
    else:
        preview_task_id = post_preview(api_key, avatar)
        prev_data = poll_task(api_key, preview_task_id, "Preview")
        save_task_json(avatar_dir, "preview", prev_data)

    if stage == "preview-only":
        glb_url = prev_data.get("model_urls", {}).get("glb")
        if glb_url:
            download_file(glb_url, avatar_dir / f"{avatar['name']}_preview.glb", "preview GLB")
        print(f"  ✅ {avatar['name']} preview done")
        return

    # Stage 2 — Refine
    refine_cache = avatar_dir / "task_refine.json"
    if refine_cache.exists():
        ref_data = json.loads(refine_cache.read_text())
        refine_task_id = ref_data["id"]
        print(f"  Refine  cached: {refine_task_id}  status={ref_data.get('status')}")
        if ref_data.get("status") != "SUCCEEDED":
            ref_data = poll_task(api_key, refine_task_id, "Refine ")
            save_task_json(avatar_dir, "refine", ref_data)
    else:
        refine_task_id = post_refine(api_key, prev_data["id"])
        ref_data = poll_task(api_key, refine_task_id, "Refine ")
        save_task_json(avatar_dir, "refine", ref_data)

    # Stage 3 — Download GLB
    model_urls = ref_data.get("model_urls", {})
    glb_url = model_urls.get("glb")
    if not glb_url:
        print("  WARNING: no GLB in refine result. Available:", list(model_urls.keys()))
        return

    glb_path = avatar_dir / f"{avatar['name']}.glb"
    if glb_path.exists():
        print(f"  GLB already present: {glb_path.name}")
    else:
        download_file(glb_url, glb_path, avatar["name"])

    # Download PBR texture maps
    texture_urls = ref_data.get("texture_urls", [])
    if texture_urls:
        tex_dir = avatar_dir / "textures"
        tex_dir.mkdir(exist_ok=True)
        for tex in texture_urls:
            for map_type, url in tex.items():
                if url:
                    ext = url.split("?")[0].rsplit(".", 1)[-1]
                    tex_path = tex_dir / f"{avatar['name']}_{map_type}.{ext}"
                    if not tex_path.exists():
                        download_file(url, tex_path, map_type)

    print(f"\n  ✅ {avatar['name']} complete  →  {glb_path}")


# ── Entry point ───────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="NEREON Meshy Avatar Generator")
    parser.add_argument(
        "--avatar", choices=list(AVATARS.keys()) + ["all"], default="all",
        help="Which avatar to generate (default: all)"
    )
    parser.add_argument(
        "--stage", choices=["preview-only", "full"], default="full",
        help="preview-only = geometry mesh only, skip refine"
    )
    args = parser.parse_args()

    api_key = load_api_key()
    print(f"Meshy API key loaded  ({api_key[:8]}...)")
    print(f"Output base: {OUTPUT_BASE}\n")

    targets = AVATARS if args.avatar == "all" else {args.avatar: AVATARS[args.avatar]}

    failed = []
    for key, avatar in targets.items():
        try:
            process_avatar(api_key, avatar, args.stage)
        except Exception as e:
            print(f"\n  ❌ {avatar['name']} FAILED: {e}")
            failed.append(avatar["name"])

    print(f"\n{'='*60}")
    if failed:
        print(f"  Completed with errors: {', '.join(failed)}")
    else:
        print("  All avatars processed successfully.")
    print(f"  GLBs in: {OUTPUT_BASE}")
    print()
    print("  Next steps in Unity:")
    print("  1. Window → Asset Database → Refresh  (or Ctrl+R)")
    print("  2. Select each GLB → Inspector → Rig tab → Avatar Type: Humanoid")
    print("  3. Click 'Configure...' → verify bone mapping (auto-detect works for A-pose)")
    print("  4. Assign AnimatorController from Assets/_NEREON/Animations/")
    print(f"{'='*60}\n")


if __name__ == "__main__":
    main()
