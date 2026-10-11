"""Read-only check that CHASE has exactly one active Blender source.

Run from any working directory: python3 Chibi/rig_tools/check_source_layout.py
Blender .blend1 backups are not alternate sources; require them to be moved
outside the active checkout before declaring a production handoff complete.
"""
from pathlib import Path

project = Path(__file__).resolve().parents[2]
authoritative = project / 'Chibi' / 'Chibi_Rigged.blend'
assert authoritative.is_file(), f'Production source missing: {authoritative}'

active_blends = sorted(project.rglob('*.blend'))
assert active_blends == [authoritative], (
    'Exactly one active .blend is permitted: ' +
    ', '.join(str(p.relative_to(project)) for p in active_blends)
)
backups = sorted(project.joinpath('Chibi').rglob('*.blend1'))
assert not backups, (
    'Move Blender .blend1 backups outside this checkout: ' +
    ', '.join(str(p.relative_to(project)) for p in backups)
)
retired = ['armor.blend', 'rig_tools/build_rig.py',
           'rig_tools/animate_validation.py', 'rig_tools/normalize_asset.py']
for relative in retired:
    assert not (project / 'Chibi' / relative).exists(), (
        f'Retired legacy workflow file reintroduced: {relative}'
    )
assert (project / 'Chibi/rig_tools/export_unity_fbx.py').is_file()
assert (project / 'Chibi/rig_tools/validate_asset.py').is_file()
print('CHASE_SOURCE_OF_TRUTH_PASS: Chibi/Chibi_Rigged.blend is the only active Blender source')
