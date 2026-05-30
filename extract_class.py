with open("src/sts2_gym/run_env.py", "r") as f:
    lines = f.readlines()

start_index = -1
for i, line in enumerate(lines):
    if line.startswith("class Sts2RunEnv"):
        start_index = i
        break

if start_index != -1:
    with open("run_env_class.txt", "w") as f:
        f.writelines(lines[start_index:])
    print(f"Extracted class from line {start_index + 1}")
else:
    print("Could not find class definition.")
