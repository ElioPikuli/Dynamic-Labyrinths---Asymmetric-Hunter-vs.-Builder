import os

# Path to the file
file_path = "/opt/homebrew/Caskroom/miniforge/base/envs/mlagents/lib/python3.10/site-packages/mlagents/trainers/settings.py"

print(f"Scanning: {file_path}")

if not os.path.exists(file_path):
    print("Error: File not found.")
    exit(1)

with open(file_path, "r") as f:
    lines = f.readlines()

new_lines = []
comment_mode = False

for line in lines:
    # We look for the specific crash-causing hooks involving "Dict["
    if "cattr.register_structure_hook" in line and ("Dict[" in line or "typing.Dict" in line):
        print(f"Commenting out broken hook: {line.strip()}")
        new_lines.append("# " + line) # Comment out the start
        comment_mode = True
        continue

    if comment_mode:
        print(f"Commenting out: {line.strip()}")
        new_lines.append("# " + line)
        if ")" in line:
            comment_mode = False # Stop commenting after the closing parenthesis
    else:
        new_lines.append(line)

with open(file_path, "w") as f:
    f.writelines(new_lines)

print("SUCCESS: File patched gently. Ready to train.")
