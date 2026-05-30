import json
with open('out_emulator.json') as f:
    data = json.load(f)
for i, step in enumerate(data['trace']):
    print(f"Step {i}: action={step['action']}, state={step['summary']['state_type']}")
