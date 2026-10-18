"""Generate a persona intro image. No API call without --generate."""
import argparse
import base64
import io
import os
from pathlib import Path

STYLES = {
    "Skuf": (
        "Heavyset shirtless russian man age 40, short hair, stubble, round face, "
        "sitting at computer desk with gaming monitor glowing, vodka bottle and shot glass on desk, "
        "plates with simple food, ordinary russian apartment room, warm tungsten indoor lighting, photorealistic. "
        "He is looking directly at camera, holding up a beer to welcome the viewer. "
        "Large bold text 'СКУФ' is written clearly at the bottom in striking letters."
    ),
    "Toxic": (
        "anime illustration, pale young adult man, age 24 in oversized black hoodie, "
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

STYLES["UwU"] = "Original adult anime streamer, age 25, pink hair, cat ear headphones, playful confident smile, pink gaming room, soft neon lighting, centered portrait, square composition. Bold readable Cyrillic text УВУ at the bottom."

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--persona", choices=list(STYLES), required=True)
    parser.add_argument("--model", default="google/gemini-2.5-flash-image")
    parser.add_argument("--output-dir", type=Path, default=Path(__file__).parent / "Assets")
    parser.add_argument("--generate", action="store_true", help="Call the paid API; otherwise print the prompt")
    parser.add_argument("--overwrite", action="store_true")
    options = parser.parse_args()
    prompt = STYLES[options.persona]
    if not options.generate:
        print(prompt)
        return
    target = options.output_dir / ("intro_" + options.persona + ".png")
    if target.exists() and not options.overwrite:
        parser.error("Output exists. Use --overwrite to replace it.")
    key = os.environ.get("OPENROUTER_API_KEY", "")
    if not key:
        parser.error("Set OPENROUTER_API_KEY in the environment.")
    import requests
    from PIL import Image
    body = {"model": options.model, "messages": [{"role": "user", "content": prompt}],
            "modalities": ["image", "text"], "image_config": {"aspect_ratio": "1:1"}}
    # Submit once. A network timeout may still represent a charged generation.
    response = requests.post("https://openrouter.ai/api/v1/chat/completions",
                             headers={"Authorization": "Bearer " + key}, json=body, timeout=90)
    response.raise_for_status()
    data = response.json()
    message = data["choices"][0]["message"]
    images = message.get("images", [])
    if not images:
        raise RuntimeError("The provider returned no image. Check the selected model and request in OpenRouter.")
    url = images[0]["image_url"]["url"]
    if not url.startswith("data:image/") or ";base64," not in url:
        raise RuntimeError("Expected a base64 image from OpenRouter.")
    binary = base64.b64decode(url.split(",", 1)[1], validate=True)
    options.output_dir.mkdir(parents=True, exist_ok=True)
    with Image.open(io.BytesIO(binary)) as image:
        image.save(target, format="PNG")
    print("Saved:", target)
    print("Reported cost:", data.get("usage", {}).get("cost", "not provided"))

if __name__ == "__main__":
    main()
