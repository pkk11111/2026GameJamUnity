"""Soap facing: offline explicit wiring of inspected test rigs. No runtime name search.
Only attaches the neutral facing/presentation to the actual player and binds existing Bite.
Preserves player physics, ground/origin transforms, GUIDs, modes and existing state/config.
"""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[5]
NS = uuid.UUID('ec50fdd5-7129-4916-925b-ce8d53c45c0c')


def guid(path):
    meta = ROOT / (path + '.meta')
    if meta.exists():
        return re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)[1]
    return uuid.uuid5(NS, path).hex


def metadata(path, folder=False):
    p = ROOT / (path + '.meta')
    if p.exists():
        return
    value = f'fileFormatVersion: 2\nguid: {guid(path)}\n'
    if folder:
        value += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    p.write_text(value, encoding='utf-8')


def blocks(text):
    return {int(m[1]): m[0] for m in re.finditer(r'^--- !u!\d+ &(\d+)(?: stripped)?\n.*?(?=^--- !u!|\Z)', text, re.M | re.S)}


def mono(ident, go, script, name, fields):
    return f'''--- !u!114 &{ident}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: {name}
{fields}'''


def wire(text):
    b = blocks(text)
    bite_ids = [ident for ident, chunk in b.items() if 'Regrowth.Gameplay.Bite::Regrowth.Gameplay.PlayerBiteAttack' in chunk]
    assert len(bite_ids) == 1
    bite = bite_ids[0]
    player = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', b[bite])[1])
    router = next(chunk for chunk in b.values() if 'Regrowth.Gameplay.Combat::Regrowth.Gameplay.PlayerAttackRouter' in chunk)
    input_id = int(re.search(r'inputSource: \{fileID: (\d+)\}', router)[1])
    if 'Regrowth.Gameplay.Player::Regrowth.Gameplay.PlayerFacing2D' not in text:
        b[player] = b[player].replace('  m_Layer:', '  - component: {fileID: 9300001}\n  - component: {fileID: 9300002}\n  m_Layer:', 1)
        # Inspected existing visual child, separate from the actual physics player/root and ground anchor.
        def transform_of(go):
            return next(int(v) for v in re.findall(r'component: \{fileID: (\d+)\}', b[go])
                        if b[int(v)].startswith('--- !u!4 '))
        player_transform = transform_of(player)
        candidates = []
        for ident, chunk in b.items():
            if not chunk.startswith('--- !u!212 ') or 'stripped' in chunk.splitlines()[0]:
                continue
            go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', chunk)[1])
            trans = transform_of(go)
            if f'm_Father: {{fileID: {player_transform}}}' in b[trans]:
                candidates.append((go, trans, ident))
        assert len(candidates) == 1, 'Expected one explicit direct presentation child'
        visual_go, visual_transform, visual_renderer = candidates[0]
        fields = f'  inputSource: {{fileID: {input_id}}}\n  deadZone: 0.05\n'
        b[9300001] = mono(9300001, player, guid('Assets/Scripts/Gameplay/Player/PlayerFacing2D.cs'),
            'Regrowth.Gameplay.Player::Regrowth.Gameplay.PlayerFacing2D', fields)
        b[9300002] = mono(9300002, player, guid('Assets/Scripts/Gameplay/Player/PlayerFacingPresentation.cs'),
            'Regrowth.Gameplay.Player::Regrowth.Gameplay.PlayerFacingPresentation',
            f'  facingSource: {{fileID: 9300001}}\n  visualRoot: {{fileID: {visual_transform}}}\n')
        # A small nose makes the otherwise symmetric gray square's visual facing observable.
        b[visual_transform] = b[visual_transform].replace('  m_Children: []', '  m_Children:\n  - {fileID: 9300011}')
        b[9300010] = '''--- !u!1 &9300010
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9300011}
  - component: {fileID: 9300012}
  m_Layer: 0
  m_Name: Facing nose - presentation only
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
'''
        b[9300011] = f'''--- !u!4 &9300011
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 9300010}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0.42, y: 0.12, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {visual_transform}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''
        nose = b[visual_renderer].replace(f'&{visual_renderer}', '&9300012').replace(f'fileID: {visual_go}', 'fileID: 9300010')
        nose = nose.replace('m_DrawMode: 0', 'm_DrawMode: 1').replace('m_Size: {x: 1, y: 1}', 'm_Size: {x: 0.2, y: 0.2}')
        nose = nose.replace('m_SortingOrder: 1', 'm_SortingOrder: 2').replace('m_Color: {r: 0.91, g: 0.9, b: 0.86, a: 1}', 'm_Color: {r: 0.3, g: 0.8, b: 1, a: 1}')
        b[9300012] = nose
    if '  facingSource:' not in b[bite]:
        b[bite] += '  facingSource: {fileID: 9300001}\n'
    for added_id in (9300001, 9300002, 9300010, 9300011, 9300012):
        b[added_id] = '\n'.join(line.rstrip() for line in b[added_id].splitlines()) + '\n'
    return text[:text.index('--- !u!')] + ''.join(b.values())


def prepare_metadata():
    (ROOT / 'Assets/Scripts/Gameplay/Player').mkdir(parents=True, exist_ok=True)
    metadata('Assets/Scripts/Gameplay/Player', True)
    for path in ('Assets/Scripts/Gameplay/Player/PlayerFacing2D.cs',
                 'Assets/Scripts/Gameplay/Player/PlayerFacingPresentation.cs',
                 'Assets/Scripts/Gameplay/Player/Regrowth.Gameplay.Player.asmdef',
                 'Assets/Scripts/Tests/T08/Tools/WirePlayerFacing.py'):
        metadata(path)


if __name__ == '__main__':
    prepare_metadata()
    for relative in ('Assets/Prefabs/Tests/T07/T07SmokeRig.prefab',
                     'Assets/Prefabs/Tests/T09/T09SmokeRig.prefab',
                     'Assets/Prefabs/Tests/T09/T09AutoChecksRig.prefab'):
        path = ROOT / relative
        old = path.read_text()
        value = wire(old)
        if old != value:
            path.write_text(value, encoding='utf-8', newline='\n')
        print(f'Explicit facing wired: {relative}')
