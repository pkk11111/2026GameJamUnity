"""Soap/T08 read-only asset references/scope validation. Not a Unity Play test.
Run with Python; Unity serialization/wiring context is documented in Soap.handoff.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[5]
ASSETS = ('Assets/Scripts/Gameplay/Sword', 'Assets/Configs/Sword', 'Assets/Prefabs/Sword',
          'Assets/Scripts/Tests/T08', 'Assets/Prefabs/Tests/T08', 'Assets/Scenes/Tests/T08')
index = {}
for base in (ROOT / 'Assets', ROOT / 'Library/PackageCache'):
    for meta in base.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig', errors='replace'), re.M)
        if match:
            index[match[1]] = Path(str(meta)[:-5])


def ids(text):
    found = re.findall(r'^--- !u!\d+ &(-?\d+)', text, re.M)
    assert len(found) == len(set(found)), 'Duplicate document IDs'
    return set(found)


for relative in ASSETS:
    folder = ROOT / relative
    assert Path(str(folder) + '.meta').is_file(), f'Missing folder meta: {folder}'
    for path in folder.rglob('*'):
        if path.suffix == '.meta':
            continue
        assert Path(str(path) + '.meta').is_file(), f'Missing meta: {path}'
        if path.suffix == '.asmdef':
            json.loads(path.read_text())
        if path.suffix not in ('.prefab', '.unity', '.asset'):
            continue
        text = path.read_text()
        local = ids(text)
        for ident in re.findall(r'\{fileID: (-?\d+)\}', text):
            assert ident == '0' or ident in local, f'Dangling local ID {ident}: {path}'
        for ident, guid in re.findall(r'\{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}', text):
            if guid.startswith('0000000000000000'):
                continue  # Unity built-ins.
            assert guid in index, f'Unknown GUID {guid}: {path}'
            target = index[guid]
            if target.suffix in ('.prefab', '.unity', '.asset'):
                target_ids = ids(target.read_text(encoding='utf-8-sig'))
                assert ident in target_ids or (target.suffix == '.prefab' and ident == '100100000'), \
                    f'Dangling external ID {ident} -> {target}: {path}'

manual = (ROOT / 'Assets/Prefabs/Tests/T08/T08SmokeRig.prefab').read_text()
auto = (ROOT / 'Assets/Prefabs/Tests/T08/T08AutoChecksRig.prefab').read_text()
scene = (ROOT / 'Assets/Scenes/Tests/T08/T08_Smoke.unity').read_text()
mount = (ROOT / 'Assets/Prefabs/Sword/SwordAttackMount.prefab').read_text()
assert '  autoRun: 0\n' in manual and '  autoRun: 1\n' in auto
assert '  testRigPrefab: {fileID: 0}\n' in auto
assert 'swordActionSource: {fileID: 9000003}' in manual
assert 'propertyPath: combatStateSource' in manual
assert 'rangeOnlyDistance: 2.8' in manual and 'Bite Cyan Jaws' in manual
assert 'source: {fileID: 1778559489657913939}' in manual
assert 'windupSeconds' not in mount and 'runContextSource' not in mount
assert 'swordOrigin: {fileID: 1005}' in mount and 'slashVisual: {fileID: 1006}' in mount
assert '  m_Enabled: 0\n' in mount and 'Collider2D:' not in mount
assert manual.count('Regrowth.Gameplay.Combat::Regrowth.Gameplay.PlayerAttackRouter') == 1
assert manual.count('Regrowth.Runtime::Regrowth.Runtime.PlayerState') == 1
assert manual.count('Regrowth.Runtime::Regrowth.Runtime.PlayerInputReader') == 1
assert manual.count('Regrowth.Gameplay.Locomotion::Regrowth.Gameplay.PlayerLocomotion') == 1
assert 'DamageDummy' not in manual
assert 'Remove Arms' in manual and 'Add Arms' in manual and 'Run T08 Auto Checks' in manual
assert '  m_Constraints: 4\n' in manual  # Manual motor can move/jump; no FreezeAll asset override.
manual_guid = re.search(r'^guid: (\w+)', (ROOT / 'Assets/Prefabs/Tests/T08/T08SmokeRig.prefab.meta').read_text(), re.M)[1]
assert manual_guid in scene
sword = (ROOT / 'Assets/Scripts/Gameplay/Sword/PlayerSwordAttack.cs').read_text()
assert 'TryConsumeAttack' not in sword and 'Time.timeScale' not in sword and 'AudioCue.PlayerBite' not in sword
assert not re.search(r'\bFind\w*\s*\(', sword)
driver = (ROOT / 'Assets/Scripts/Tests/T08/T08SmokeDriver.cs').read_text()
checks = len(re.findall(r'\bCheck\(', driver)) - 1  # Exclude Check method declaration.
print(f'T08 static asset/reference/meta/unique-player/Manual-Auto checks OK; {checks} planned Play assertions, NOT run.')
