import time, json, base64, struct, requests, os

API_KEY = os.environ.get("OPENROUTER_API_KEY", "")
URL = "https://openrouter.ai/api/v1/chat/completions"
H = {"Authorization": f"Bearer {API_KEY}", "Content-Type": "application/json"}
OUT = os.path.join(os.path.dirname(__file__), "Assets")

STYLES = {
    "Skuf": (
        "Heavyset shirtless russian man age 40, short hair, stubble, round face, "
        "sitting at computer desk with gaming monitor glowing, vodka bottle and shot glass on desk, "
        "plates with simple food, ordinary russian apartment room, warm tungsten indoor lighting, photorealistic. "
        "He is looking directly at camera, holding up a beer to welcome the viewer. "
        "Large bold text 'СКУФ' is written clearly at the bottom in striking letters."
    ),
    "Toxic": (
        "anime illustration, skinny pale teenage boy in oversized black hoodie, "
        "hood up half-covering face, messy dark hair with white streak, "
        "red glowing eyes visible under hood, headphones around neck, "
        "sitting in dark room at messy desk with gaming keyboard and mouse, "
        "monster energy cans scattered, cigarette in hand, "
        "edgy dark aesthetic, high contrast, neon red accents on black. "
        "He is staring intensely at the camera, radiating dark aura. "
        "Large edgy neon text 'ТОКСИК' is written clearly at the bottom."
    ),
    "Mistress": (
        "Arcane animated series painterly 3D art style by Fortiche studio, "
        "tall elegant beautiful dominant woman, extremely feminine but imposing, "
        "wearing a stylish black velvet evening gown with a high thigh slit revealing her long legs in sheer black tights and high heels, "
        "sitting gracefully with legs crossed on a massive iron dungeon throne, "
        "subtle gachimuchi references like heavy chains in the blurry background, "
        "dark moody environment, neon magenta and deep purple rim lighting, "
        "arrogant mocking expression, looking down at viewer, masterpiece. "
        "Large elegant serif text 'ГОСПОЖА' is written clearly at the bottom."
    )
}

print("="*50)
print("  GENERATING INTRO IMAGES FOR PERSONAS")
print("="*50)

total_cost = 0

for persona, prompt_text in STYLES.items():
    if persona != "Mistress": continue  # Only regenerate Mistress
    print(f"\n[IMG] Generating Intro for {persona}...")
    t0 = time.time()
    
    body = {
        "model": "google/gemini-3.1-flash-image-preview",
        "messages": [{"role": "user", "content": f"Generate image: {prompt_text}"}],
        "modalities": ["image", "text"],
        "image_config": {"aspect_ratio": "1:1", "image_size": "1K"}  # 1K is standard ~1.5MB file
    }
    
    r2 = requests.post(URL, headers=H, json=body, timeout=90)
    dt2 = time.time() - t0

    if r2.status_code != 200:
        print(f"  FAIL {r2.status_code}: {r2.text[:150]}")
        continue

    d2 = r2.json()
    img_cost = d2.get("usage", {}).get("cost", 0)
    total_cost += img_cost

    msg2 = d2["choices"][0]["message"]
    b64 = None
    if msg2.get("images"):
        b64 = msg2["images"][0]["image_url"]["url"]
    if not b64 and isinstance(msg2.get("content"), list):
        for p2 in msg2["content"]:
            if p2.get("type") == "image_url":
                b64 = p2["image_url"]["url"]
                break
    if not b64:
        print("  NO IMAGE (safety filter?)")
        continue

    rawb = b64.split(",", 1)[-1] if "," in b64 else b64
    ib = base64.b64decode(rawb)
    
    # Check dimensions
    w, h = struct.unpack('>II', ib[16:24]) if ib[:4] == b'\x89PNG' else (0, 0)
    
    fn = f"intro_{persona}.png"
    out_path = os.path.join(OUT, fn)
    with open(out_path, "wb") as f:
        f.write(ib)
    
    print(f"  {w}x{h}px | {len(ib)/1024:.0f}KB | {dt2:.1f}s | ${img_cost:.4f}")
    print(f"  Saved: {out_path}")

print(f"\nTOTAL COST: ${total_cost:.4f}")
