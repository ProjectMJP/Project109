import os
import re
import json

relics_yaml_path = r"e:\unity\Project109\Project109\Assets\Data\Relics.yaml"
output_yaml_dir = r"e:\unity\Project109\Project109\Assets\StreamingAssets\Mods\Core\YAML\Relics"
output_lua_dir = r"e:\unity\Project109\Project109\Assets\StreamingAssets\Mods\Core\Scripts\Relics"

# 출력 폴더 생성
os.makedirs(output_yaml_dir, exist_ok=True)
os.makedirs(output_lua_dir, exist_ok=True)

# Relics.yaml 파일 읽기
with open(relics_yaml_path, "r", encoding="utf-8") as f:
    content = f.read()

# 수동으로 유물 리스트 분리
# "  - " 로 나눈 뒤, relicName 필드가 있는 것만 유물 데이터로 취급
blocks = content.split("  - ")
relics = []

for block in blocks:
    if not block.strip() or "relicCollection:" in block:
        continue
    
    lines = block.split("\n")
    relic = {}
    for line in lines:
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        
        # 키와 값 분리
        m = re.match(r"^([a-zA-Z0-9_]+)\s*:\s*(.*)$", line)
        if m:
            key = m.group(1)
            val = m.group(2).strip()
            
            # !!int, !!bool 등 타입 강제 지정 제거
            val = re.sub(r"^!![a-z]+\s+", "", val)
            
            # 따옴표가 있는 경우 제거
            if (val.startswith('"') and val.endswith('"')) or (val.startswith("'") and val.endswith("'")):
                val = val[1:-1]
            
            relic[key] = val
            
    if "relicName" in relic:
        relics.append(relic)

# 등급 맵핑 규칙 (1->Common, 2->Rare, 3->Unique, 4->Boss)
rarity_map = {
    "1": "Common",
    "2": "Rare",
    "3": "Unique",
    "4": "Boss"
}

print(f"Total found relics in raw yaml: {len(relics)}")

for relic in relics:
    name = relic.get("relicName")
    rarity_num = relic.get("rarity", "1")
    rarity_str = rarity_map.get(rarity_num, "Common")
    description = relic.get("description", "")
    
    # 1. YAML 파일 작성
    yaml_content = f'''relicName: {json.dumps(name, ensure_ascii=False)}
classTypes:
  - "All"
rarity: "{rarity_str}"
upgradedRelicId: ""
description: {json.dumps(description, ensure_ascii=False)}
flavorText: ""
'''
    yaml_file_path = os.path.join(output_yaml_dir, f"{name}.yaml")
    with open(yaml_file_path, "w", encoding="utf-8") as wf:
        wf.write(yaml_content)
        
    # 2. Lua 파일 작성
    lua_content = f'''local relic = {{}}

-- 유물 초기화 (획득 시 1회 호출)
function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 전투 시작 시 호출 (IOnBattleStart 대응)
-- 효과: {description}
function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end

    -- TODO: 여기에 유물 효과 구현
end

return relic
'''
    lua_file_path = os.path.join(output_lua_dir, f"{name}.lua")
    with open(lua_file_path, "w", encoding="utf-8") as lf:
        lf.write(lua_content)

print(f"Successfully generated {len(relics)} relics in YAML and Lua folders.")
