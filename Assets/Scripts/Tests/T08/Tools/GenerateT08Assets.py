"""Soap/T08 offline asset setup: no Unity/GUI, no changes to existing T07/T09 assets.

Uses inspected Unity 6000.2.9f1 T09 serialization as a read-only template.
Existing differing outputs are refused by default; --update explicitly rewrites this module's
owned generated assets after review. It never writes source T09 assets.
Run with Python from any cwd. Rules/wiring: AGENTS.md and Soap.handoff.
"""
from pathlib import Path
import re
import uuid
import sys
from WirePlayerFacing import prepare_metadata, wire
prepare_metadata()
UPDATE = "--update" in sys.argv

ROOT = Path(__file__).resolve().parents[5]
assert (ROOT / 'AGENTS.md').is_file(), ROOT
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
NAMESPACE = uuid.UUID('ec50fdd5-7129-4916-925b-ce8d53c45c0c')


def guid(path):
    meta = ROOT / (path + '.meta')
    if meta.exists():
        return re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)[1]
    return uuid.uuid5(NAMESPACE, path).hex


def write(path, text):
    output = ROOT / path
    if output.exists() and output.read_text(encoding='utf-8-sig') != text and not UPDATE:
        raise RuntimeError(f'Refusing to overwrite existing differing output: {path}')
    output.parent.mkdir(parents=True, exist_ok=True)
    if not output.exists() or UPDATE:
        output.write_text(text, encoding='utf-8', newline='\n')


def meta(path, folder=False):
    text = f'fileFormatVersion: 2\nguid: {guid(path)}\n'
    if folder:
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.endswith('.prefab'):
        text += 'PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.endswith('.asset'):
        text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    write(path + '.meta', text)


def blocks(text):
    return {int(m[2]): m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n.*?(?=^--- !u!|\Z)', text, re.M | re.S)}


def common(go):
    return f'  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'


def game_object(ident, name, components):
    return f'--- !u!1 &{ident}\nGameObject:\n' + common(ident).replace(f'  m_GameObject: {{fileID: {ident}}}\n', '') + '  serializedVersion: 6\n  m_Component:\n' + ''.join(f'  - component: {{fileID: {c}}}\n' for c in components) + f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'


def transform(ident, go, parent, children=(), x=0):
    return f'--- !u!4 &{ident}\nTransform:\n' + common(go) + f'  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: {x}, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n' + ('  m_Children:\n' + ''.join(f'  - {{fileID: {c}}}\n' for c in children) if children else '  m_Children: []\n') + f'  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'


for directory in ('Assets/Scripts/Gameplay/Sword', 'Assets/Configs/Sword', 'Assets/Prefabs/Sword',
                  'Assets/Scripts/Tests/T08', 'Assets/Scripts/Tests/T08/Tools',
                  'Assets/Prefabs/Tests/T08', 'Assets/Scenes/Tests/T08'):
    (ROOT / directory).mkdir(parents=True, exist_ok=True)
    meta(directory, True)
for path in ('Assets/Scripts/Gameplay/Sword/PlayerSwordAttack.cs', 'Assets/Scripts/Gameplay/Sword/SwordConfig.cs',
             'Assets/Scripts/Gameplay/Bite/PlayerBiteFlash.cs',
             'Assets/Scripts/Gameplay/Sword/Regrowth.Gameplay.Sword.asmdef',
             'Assets/Scripts/Tests/T08/T08SmokeDriver.cs', 'Assets/Scripts/Tests/T08/Regrowth.Tests.T08.asmdef',
             'Assets/Scripts/Tests/T08/Tools/GenerateT08Assets.py',
             'Assets/Scripts/Tests/T08/Tools/ValidateT08Assets.py'):
    meta(path)

config_path = 'Assets/Configs/Sword/SwordConfig.asset'
mount_path = 'Assets/Prefabs/Sword/SwordAttackMount.prefab'
manual_path = 'Assets/Prefabs/Tests/T08/T08SmokeRig.prefab'
auto_path = 'Assets/Prefabs/Tests/T08/T08AutoChecksRig.prefab'
scene_path = 'Assets/Scenes/Tests/T08/T08_Smoke.unity'
script = guid('Assets/Scripts/Gameplay/Sword/PlayerSwordAttack.cs')
config_script = guid('Assets/Scripts/Gameplay/Sword/SwordConfig.cs')
driver_script = guid('Assets/Scripts/Tests/T08/T08SmokeDriver.cs')

settings = HEADER + '--- !u!114 &11400000\nMonoBehaviour:\n' + common(0) + f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {config_script}, type: 3}}\n  m_Name: SwordConfig\n  m_EditorClassIdentifier: Regrowth.Gameplay.Sword::Regrowth.Gameplay.SwordConfig\n  attackRange: 1.8\n  cooldownSeconds: 0.6\n  targetLayers:\n    serializedVersion: 2\n    m_Bits: 1\n  includeTriggers: 1\n'
write(config_path, settings)
meta(config_path)

# Formal mount deliberately requires the integrator to bind the actual player and Run ports.
# It owns no player state, input, motor or collider; its only visual is a disabled gold slash.
enemy_blocks = blocks((ROOT / 'Assets/Prefabs/EnemyBasic/EnemyBasic.prefab').read_text())
renderer = enemy_blocks[205179182264572143]
renderer = renderer.replace('&205179182264572143', '&1006').replace('fileID: 7975838277919623124', 'fileID: 1004')
renderer = renderer.replace('  m_Enabled: 1\n', '  m_Enabled: 0\n').replace('  m_Size: {x: 1, y: 1}', '  m_Size: {x: 1.6, y: 0.12}')
renderer = renderer.replace('  m_Color: {r: 0.55, g: 0.45, b: 0.4, a: 1}', '  m_Color: {r: 1, g: 0.85, b: 0.25, a: 1}').replace('  m_SortingOrder: 0', '  m_SortingOrder: 5')
action = '--- !u!114 &1003\nMonoBehaviour:\n' + common(1001) + f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Regrowth.Gameplay.Sword::Regrowth.Gameplay.PlayerSwordAttack\n  combatStateSource: {{fileID: 0}}\n  swordOrigin: {{fileID: 1005}}\n  facingSource: {{fileID: 0}}\n  config: {{fileID: 11400000, guid: {guid(config_path)}, type: 2}}\n  slashVisual: {{fileID: 1006}}\n  flashSeconds: 0.12\n'
renderer = renderer.replace('fileID: 1004', 'fileID: 1010')
mount = HEADER + game_object(1001, 'Sword Attack Mount - bind actual player', (1002, 1003)) + transform(1002, 1001, 0, (1005,)) + action + game_object(1004, 'Sword Origin - reference offset', (1005,)) + transform(1005, 1004, 1002, (1011,), x=0.65) + game_object(1010, 'Sword Slash - facing presentation', (1011, 1006)) + transform(1011, 1010, 1005) + renderer
write(mount_path, mount)
meta(mount_path)

original = wire((ROOT / 'Assets/Prefabs/Tests/T09/T09SmokeRig.prefab').read_text())
rig = blocks(original)
assert len(rig) == len(re.findall(r'^--- !u!', original, re.M)), 'Duplicate source IDs'
# Clone the inspected Auto button and its own child objects, with remapped local references only.
button_go, button_transform, button_component = 2507528877815820196, 2095109719430398221, 3787175313059015829
button_child_transform = 5134158877569583221
button_child_go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', rig[button_child_transform])[1])
button_ids = [button_go] + [int(v) for v in re.findall(r'component: \{fileID: (\d+)\}', rig[button_go])]
button_ids += [button_child_go] + [int(v) for v in re.findall(r'component: \{fileID: (\d+)\}', rig[button_child_go])]
assert len(set(button_ids)) == len(button_ids)
new_buttons = []


def clone_button(label, x, base):
    mapping = {old: base + i for i, old in enumerate(button_ids)}
    for old in button_ids:
        chunk = rig[old]
        chunk = re.sub(r'(?<=&)(\d+)|(?<=fileID: )(\d+)', lambda m: str(mapping.get(int(m[0]), int(m[0]))), chunk)
        chunk = chunk.replace('Run T09 Auto Checks - fresh isolated session', label).replace('Run T09 Auto Checks', label)
        if old == button_transform:
            chunk = chunk.replace('m_AnchoredPosition: {x: 0, y: 20}', f'm_AnchoredPosition: {{x: {x}, y: 20}}')
        new_buttons.append(chunk)
    return mapping[button_transform], mapping[button_component]


remove_transform, remove_button = clone_button('Remove Arms', -350, 9100100)
add_transform, add_button = clone_button('Add Arms', 350, 9100200)
rig[7630176805854379811] = rig[7630176805854379811].replace('  m_Father:', f'  - {{fileID: {remove_transform}}}\n  - {{fileID: {add_transform}}}\n  m_Father:', 1)
rig[3938566807945809415] = rig[3938566807945809415].replace('  m_Father:', '  - {fileID: 9000002}\n  m_Father:', 1)
rig[1431967242288177660] = rig[1431967242288177660].replace('swordActionSource: {fileID: 0}', 'swordActionSource: {fileID: 9000003}')
driver = rig[4535985388146873472]
driver = driver.replace('6945e0fd9c5245dfbf7267283555c674', driver_script).replace('Regrowth.Tests.T09::Regrowth.Tests.T09.T09SmokeDriver', 'Regrowth.Tests.T08::Regrowth.Tests.T08.T08SmokeDriver')
driver = re.sub(r'^  (config|registration):.*\n', '', driver, flags=re.M)
driver = driver.replace('  body:', f'  sword: {{fileID: 9000003}}\n  swordConfig: {{fileID: 11400000, guid: {guid(config_path)}, type: 2}}\n  slashVisual: {{fileID: 9000004}}\n  body:', 1)
driver = driver.replace('  testRigRoot:', f'  removeArmsButton: {{fileID: {remove_button}}}\n  addArmsButton: {{fileID: {add_button}}}\n  testRigRoot:', 1)
driver = re.sub(r'^  testRigPrefab:.*$', f'  testRigPrefab: {{fileID: 3258771513088229276, guid: {guid(auto_path)}, type: 3}}', driver, flags=re.M)
driver = driver.replace('  body:', '  facing: {fileID: 9300001}\n  visualRoot: {fileID: 5367696411087812162}\n  groundOrigin: {fileID: 6678514365033580998}\n  enemyLeft: {fileID: 9400003}\n  biteVisual: {fileID: 9200003}\n  biteOrigin: {fileID: 6917685865574849787}\n  swordOrigin: {fileID: 9000005}\n  rangeOnlyDistance: 2.8\n  body:', 1)
rig[4535985388146873472] = driver
# Cyan vertical jaws: presentation subscribes to accepted real Bite, owns no damage.
rig[4928783787434640725] = rig[4928783787434640725].replace('  m_Layer:', '  - component: {fileID: 9200004}\n  m_Layer:', 1)
rig[6917685865574849787] = rig[6917685865574849787].replace('  m_Children: []', '  m_Children:\n  - {fileID: 9200002}')
rig[7139558766832064651] = rig[7139558766832064651].replace('      value: 3\n', '      value: 2.8\n')
rig[6139287686119166189] = rig[6139287686119166189].replace('y: 355', 'y: 320').replace('y: 150', 'y: 220')
bite_renderer = renderer.replace('&1006', '&9200003').replace('fileID: 1010', 'fileID: 9200001').replace('m_Size: {x: 1.6, y: 0.12}', 'm_Size: {x: 0.22, y: 0.6}').replace('m_Color: {r: 1, g: 0.85, b: 0.25, a: 1}', 'm_Color: {r: 0.2, g: 0.95, b: 1, a: 1}')
bite_flash = '--- !u!114 &9200004\nMonoBehaviour:\n' + common(4928783787434640725) + f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid("Assets/Scripts/Gameplay/Bite/PlayerBiteFlash.cs")}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Regrowth.Gameplay.Bite::Regrowth.Gameplay.PlayerBiteFlash\n  source: {{fileID: 1778559489657913939}}\n  visual: {{fileID: 9200003}}\n  flashSeconds: 0.12\n'
bite_presentation = game_object(9200001, 'Bite Cyan Jaws - presentation only', (9200002, 9200003)) + transform(9200002, 9200001, 6917685865574849787) + bite_renderer + bite_flash

# Nested formal mount. Stripped local IDs allow Router/driver to bind inherited components.
mount_guid = guid(mount_path)
nested = f'''--- !u!1001 &9000001
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 3938566807945809415}}
    m_Modifications:
    - target: {{fileID: 1003, guid: {mount_guid}, type: 3}}
      propertyPath: combatStateSource
      value: 
      objectReference: {{fileID: 1998222914354218060}}
    - target: {{fileID: 1003, guid: {mount_guid}, type: 3}}
      propertyPath: facingSource
      value:
      objectReference: {{fileID: 9300001}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {mount_guid}, type: 3}}
--- !u!4 &9000002 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 1002, guid: {mount_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 9000001}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &9000003 stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: 1003, guid: {mount_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 9000001}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Regrowth.Gameplay.Sword::Regrowth.Gameplay.PlayerSwordAttack
--- !u!4 &9000005 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 1005, guid: {mount_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 9000001}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!212 &9000004 stripped
SpriteRenderer:
  m_CorrespondingSourceObject: {{fileID: 1006, guid: {mount_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 9000001}}
  m_PrefabAsset: {{fileID: 0}}
'''
# Mirror the existing real EnemyBasic instance for manual left/right acceptance.
left_mapping = {7139558766832064651: 9400001, 1113573351264691579: 9400002, 4911819158684606080: 9400003}
left_enemy = ''
for old_id in left_mapping:
    chunk = rig[old_id]
    chunk = re.sub(r'(?<=&)(\d+)|(?<=fileID: )(\d+)', lambda m: str(left_mapping.get(int(m[0]), int(m[0]))), chunk)
    chunk = chunk.replace('      value: 2.8\n', '      value: -2.8\n')
    left_enemy += '\n'.join(line.rstrip() for line in chunk.splitlines()) + '\n'
rig[7906253095266570849] = rig[7906253095266570849].replace('  m_Father:', '  - {fileID: 9400002}\n  m_Father:', 1)
manual = HEADER + ''.join(rig.values()) + ''.join(new_buttons) + bite_presentation + nested + left_enemy
manual = manual.replace('T09SmokeRig', 'T08SmokeRig').replace('T09 Real Player - Head then BodyCore', 'T08 Real Player - Body Arms no Legs').replace('T07 Test UI', 'T08 Test UI').replace('T09 MANUAL', 'T08 MANUAL').replace('Run T09 Auto Checks', 'Run T08 Auto Checks')
assert 'Regrowth.Tests.T09::Regrowth.Tests.T09.T09SmokeDriver' not in manual
write(manual_path, manual)
meta(manual_path)
auto = manual.replace('T08SmokeRig', 'T08AutoChecksRig').replace('  autoRun: 0', '  autoRun: 1')
auto = re.sub(r'^  testRigPrefab:.*$', '  testRigPrefab: {fileID: 0}', auto, flags=re.M)
write(auto_path, auto)
meta(auto_path)
scene = (ROOT / 'Assets/Scenes/Tests/T09/T09_Smoke.unity').read_text().replace('7b76bdf24168d8b4fb783f5a21bd5ea4', guid(manual_path)).replace('T09 Minimal Smoke Rig', 'T08 Manual Smoke Rig')
write(scene_path, scene)
meta(scene_path)
for path in (mount_path, manual_path, auto_path, scene_path):
    serialized = (ROOT / path).read_text()
    ids = re.findall(r'^--- !u!\d+ &(\d+)', serialized, re.M)
    assert len(ids) == len(set(ids)), f'Duplicate local IDs: {path}'
print('T08 assets saved offline: explicit Sword mount, fresh Manual/Auto rigs, scene, config and paired metas.')
