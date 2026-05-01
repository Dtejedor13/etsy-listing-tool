import os
import json
import requests
import webbrowser
import hashlib
import base64
import secrets
from http.server import HTTPServer, BaseHTTPRequestHandler
import urllib.parse

# ==== CONFIG ====
BASE_PATH = "products"
SHOP_ID = "..."

CLIENT_ID = "..."
CLIENT_SECRET = "..."

REDIRECT_URI = "http://localhost:8080/callback"
SCOPES = "listings_w listings_r shops_r"

TOKEN_FILE = "tokens.json"

# ==== PKCE ====
def generate_pkce():
    code_verifier = base64.urlsafe_b64encode(secrets.token_bytes(32)).decode().rstrip("=")
    code_challenge = base64.urlsafe_b64encode(
        hashlib.sha256(code_verifier.encode()).digest()
    ).decode().rstrip("=")
    return code_verifier, code_challenge

# ==== TOKEN STORAGE ====
def save_tokens(data):
    with open(TOKEN_FILE, "w") as f:
        json.dump(data, f)

def load_tokens():
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE, "r") as f:
            return json.load(f)
    return None

# ==== CALLBACK SERVER ====
class CallbackHandler(BaseHTTPRequestHandler):
    auth_code = None

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        query = urllib.parse.parse_qs(parsed.query)

        if "code" in query:
            CallbackHandler.auth_code = query["code"][0]

        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"Auth success. You can close this window.")

def get_auth_code(code_challenge):
    url = (
        "https://www.etsy.com/oauth/connect?"
        + urllib.parse.urlencode({
            "response_type": "code",
            "client_id": CLIENT_ID,
            "redirect_uri": REDIRECT_URI,
            "scope": SCOPES,
            "code_challenge": code_challenge,
            "code_challenge_method": "S256"
        })
    )

    print("Opening browser...")
    webbrowser.open(url)

    server = HTTPServer(("localhost", 8080), CallbackHandler)
    server.handle_request()

    return CallbackHandler.auth_code

# ==== TOKEN REQUEST ====
def exchange_code(code, verifier):
    url = "https://api.etsy.com/v3/public/oauth/token"

    data = {
        "grant_type": "authorization_code",
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "code": code,
        "code_verifier": verifier
    }

    return requests.post(url, data=data).json()

def get_access_token():
    tokens = load_tokens()

    if tokens:
        return tokens["access_token"]

    verifier, challenge = generate_pkce()
    code = get_auth_code(challenge)

    token_data = exchange_code(code, verifier)
    save_tokens(token_data)

    return token_data["access_token"]

# ==== HEADERS ====
def get_headers(token):
    return {
        "x-api-key": CLIENT_ID,
        "Authorization": f"Bearer {token}"
    }

# ==== EXISTING LOGIC ====
def get_product_folders(base_path):
    return [
        os.path.join(base_path, name)
        for name in os.listdir(base_path)
        if os.path.isdir(os.path.join(base_path, name))
    ]

def load_settings(folder_path):
    with open(os.path.join(folder_path, "settings.json"), "r", encoding="utf-8") as f:
        return json.load(f)

def get_images(folder_path):
    images_path = os.path.join(folder_path, "images")
    files = [
        f for f in os.listdir(images_path)
        if os.path.isfile(os.path.join(images_path, f))
    ]
    files.sort(key=lambda x: int(os.path.splitext(x)[0]))
    return [os.path.join(images_path, f) for f in files]

def create_listing(data, headers):
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

    res = requests.post(url, json=payload, headers=headers)

    if res.status_code != 201:
        print("error:", res.text)
        return None

    return res.json()

def upload_images(listing_id, image_paths, headers):
    for i, img_path in enumerate(image_paths):
        url = f"https://openapi.etsy.com/v3/application/shops/{SHOP_ID}/listings/{listing_id}/images"

        with open(img_path, "rb") as f:
            files = {"image": f}
            data = {"rank": i + 1}

            res = requests.post(url, headers=headers, files=files, data=data)

            if res.status_code != 201:
                print("upload error:", res.text)

# ==== MAIN ====
if __name__ == "__main__":
    token = get_access_token()
    headers = get_headers(token)

    folders = get_product_folders(BASE_PATH)

    for folder in folders:
        print(f"processing: {folder}")

        try:
            settings = load_settings(folder)
            images = get_images(folder)

            listing = create_listing(settings, headers)

            if listing:
                upload_images(listing["listing_id"], images, headers)
                print("draft created!")

        except Exception as e:
            print("error:", e)