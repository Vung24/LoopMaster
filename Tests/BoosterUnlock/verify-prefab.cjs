const fs = require('node:fs');
const assert = require('node:assert/strict');
const path = 'Assets/00Game/Resources/BoosterPresentation.prefab';
const yaml = fs.readFileSync(path, 'utf8');
const validateMetaGuid = assetPath => {
    const meta = fs.readFileSync(assetPath + '.meta', 'utf8');
    const match = meta.match(/^guid: (\S+)\r?$/m);
    assert(match && /^[0-9a-f]{32}$/.test(match[1]), `Invalid Unity GUID in ${assetPath}.meta`);
    return match[1];
};
validateMetaGuid(path);
const configPath = 'Assets/00Game/Resources/BoosterUnlockConfig.asset';
validateMetaGuid(configPath);
const config = fs.readFileSync(configPath, 'utf8');
const configScriptGuid = validateMetaGuid('Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterUnlockConfig.cs');
validateMetaGuid('Assets/00Game/01Scripts/02Gameplay/Boosters/BoosterDefinition.cs');
assert(config.includes(`guid: ${configScriptGuid},`), 'Config asset script does not match BoosterUnlockConfig');
const keys = [...config.matchAll(/^  - _key: (.+)$/gm)].map(m => m[1].trim());
const levels = [...config.matchAll(/^    _unlockLevel: (\d+)$/gm)].map(m => Number(m[1]));
assert(keys.length > 0 && keys.every(Boolean), 'Config must define booster keys');
assert.equal(new Set(keys).size, keys.length, 'Duplicate booster keys');
assert.equal(levels.length, keys.length, 'Every booster requires an unlock level');
assert(levels.every(l => l > 0) && new Set(levels).size === levels.length, 'Invalid or duplicate unlock levels');
for (const match of yaml.matchAll(/guid: ([^,}\s]+)/g)) {
    assert(/^[0-9a-f]{32}$/.test(match[1]), `Invalid GUID reference in prefab: ${match[1]}`);
}
const objects = new Map();
for (const match of yaml.matchAll(/--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=--- !u!|$)/g)) {
    assert(!objects.has(match[2]), `Duplicate fileID ${match[2]}`);
    objects.set(match[2], { type: Number(match[1]), body: match[3] });
}
assert(objects.size > 0, 'Prefab contains no serialized objects');
for (const [id, object] of objects) {
    for (const match of object.body.matchAll(/\{fileID: (\d+)\}/g)) {
        if (match[1] !== '0') assert(objects.has(match[1]), `Object ${id} references missing fileID ${match[1]}`);
    }
    if (object.type === 1) {
        for (const match of object.body.matchAll(/component: \{fileID: (\d+)\}/g)) {
            assert(objects.get(match[1]).body.includes(`m_GameObject: {fileID: ${id}}`),
                `Component ${match[1]} belongs to a different GameObject`);
        }
    }
}
const findScript = name => {
    const guid = validateMetaGuid(`Assets/00Game/01Scripts/02Gameplay/Boosters/${name}.cs`);
    const matches = [...objects.values()].filter(o => o.body.includes(`guid: ${guid},`));
    assert.equal(matches.length, 1, `Expected one ${name} component`);
    return matches[0].body;
};
const field = (body, name, type) => {
    const match = body.match(new RegExp(`  ${name}: \\{fileID: (\\d+)\\}`));
    assert(match, `Missing serialized field ${name}`);
    const target = objects.get(match[1]);
    assert(target && target.type === type, `Invalid serialized field ${name}`);
    return target;
};
const presentation = findScript('BoosterPresentationUI');
field(presentation, '_overlayCanvas', 223);
field(presentation, '_inputGroup', 225);
for (const name of ['_dialog', '_guide', '_flightIcon']) field(presentation, name, 114);
const dialog = findScript('BoosterUnlockDialog');
for (const name of ['_icon', '_glow', '_titleText', '_prompt', '_button', '_descriptionText']) field(dialog, name, 114);
field(dialog, '_group', 225);
const guide = findScript('BoosterTutorialGuide');
field(guide, '_root', 224);
field(guide, '_focus', 114);
field(guide, '_tooltip', 114);
field(guide, '_tooltipPanel', 224);
field(guide, '_focusGlow', 114);
field(guide, '_circleMask', 114);
const circleMask = findScript('BoosterFocusMask');
assert(circleMask.includes('m_RaycastTarget: 0'), 'Circular dim must not intercept the focus button');
const shades = guide.match(/  _shades:\r?\n((?:  - \{fileID: \d+\}\r?\n)+)/);
assert(shades && [...shades[1].matchAll(/fileID:/g)].length === 4, 'Expected four wired mask shades');
for (const name of ['Dialog', 'FlightIcon', 'Tutorial']) {
    const node = [...objects.values()].find(o => o.type === 1 && o.body.includes(`m_Name: ${name}\n`));
    assert(node && node.body.includes('m_IsActive: 0'), `${name} must start inactive`);
}
for (const name of ['IntroPanel', 'TooltipPanel']) {
    const node = [...objects.values()].find(o => o.type === 1 && o.body.includes(`m_Name: ${name}\n`));
    assert(node, `Missing layout container ${name}`);
    for (const match of node.body.matchAll(/component: \{fileID: (\d+)\}/g)) {
        assert(!objects.get(match[1]).body.includes('guid: fe87c0e1cc204ed48ad3b37840f39efc'),
            `${name} must remain a text layout container without a colored background`);
    }
}
for (const name of ['BoosterPresentationUI', 'BoosterUnlockDialog', 'BoosterTutorialGuide', 'BoosterFlyPresenter']) {
    const source = fs.readFileSync(`Assets/00Game/01Scripts/02Gameplay/Boosters/${name}.cs`, 'utf8');
    assert(!/new GameObject|AddComponent</.test(source), `${name} still builds UI at runtime`);
}
console.log(`Booster prefab verification passed: ${objects.size} objects, all local and serialized references valid; no runtime UI builders.`);
