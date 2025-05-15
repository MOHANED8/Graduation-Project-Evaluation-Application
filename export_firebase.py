import firebase_admin
from firebase_admin import credentials, firestore
import json

cred = credentials.Certificate(
    r"D:\IKU\Software Engineering\Lab\SoftwareProjectson\SoftwareProject\evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json"
)

firebase_admin.initialize_app(cred)

db = firestore.client()

# Get all collections
collections = db.collections()

export_data = {}

for collection in collections:
    docs = collection.stream()
    export_data[collection.id] = {doc.id: doc.to_dict() for doc in docs}

# Save to JSON
with open('firestore_export.json', 'w', encoding='utf-8') as f:
    json.dump(export_data, f, ensure_ascii=False, indent=4)

print("Firestore export completed.")
