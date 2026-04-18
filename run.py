import os
import json
import requests

# ==== CONFIG ====
BASE_PATH = "products"
API_KEY = "..."
ACCESS_TOKEN = "..."
SHOP_ID = "..."

HEADERS = {
    "x-api-key": API_KEY,
    "Authorization": f"Bearer {ACCESS_TOKEN}"
}

def get_product_folders(base_path):
    """loads all different folders for the different new drafts.
    Each folder equals one draft/listing 
    and contains images and data for the listing."""
    return [
        os.path.join(base_path, name)
        for name in os.listdir(base_path)
        if os.path.isdir(os.path.join(base_path, name))
    ]


def load_settings(folder_path):
    settings_path = os.path.join(folder_path, "settings.json")
    with open(settings_path, "r", encoding="utf-8") as f:
        return json.load(f)

def get_images(folder_path):
    images_path = os.path.join(folder_path, "images")
    files = [
        f for f in os.listdir(images_path)
        if os.path.isfile(os.path.join(images_path, f))
    ]
    # the first image will be called 0 and is the thumbnail
    files.sort(key=lambda x: int(os.path.splitext(x)[0]))
    return [os.path.join(images_path, f) for f in files]

def create_listing(data):
    url = f"https://openapi.etsy.com/v3/application/shops/{SHOP_ID}/listings"
    payload = {
        "title": data["title"],
        "description": data["description"],
        "price": data["price"],
        "quantity": data.get("quantity", 10),
        "who_made": "i_did",
        "when_made": "made_to_order",
        "taxonomy_id": 1,
        "state": "draft",
        "tags": data["tags"]
    }

    response = requests.post(url, json=payload, headers=HEADERS)
    
    if response.status_code != 201:
        print("error:", response.text)
        return None
    
    return response.json()

def upload_images(listing_id, image_paths):
    for i, img_path in enumerate(image_paths):
        url = f"https://openapi.etsy.com/v3/application/shops/{SHOP_ID}/listings/{listing_id}/images"
        with open(img_path, "rb") as f:
            files = {"image": f}
            data = {
                "rank": i + 1
            }
            response = requests.post(url, headers=HEADERS, files=files, data=data)
            if response.status_code != 201:
                print(f"upload error for ({img_path}):", response.text)


if __name__ == "__main__":
    folders = get_product_folders(BASE_PATH)

    for folder in folders:
        print(f"current dirfectory: {folder}")

        try:
            settings = load_settings(folder)
            images = get_images(folder)

            listing = create_listing(settings)

            if listing:
                listing_id = listing["listing_id"]
                upload_images(listing_id, images)
                print(f"draft created: {listing_id}!")

        except Exception as e:
            print("error", e)