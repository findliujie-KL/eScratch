const assert = require('node:assert/strict');
const { test } = require('node:test');
const { Arch } = require('builder-util');
const { Platform } = require('app-builder-lib');
const { computeArchToTargetNamesMap } = require('app-builder-lib/out/targets/targetFactory');
const { build } = require('../package.json');

// Exercise the installed packager's target selection for the workflow's
// `electron-builder --mac` command on either runner architecture.
for (const hostArch of [Arch.x64, Arch.arm64]) {
  test(`Mac release selects both DMGs on a ${Arch[hostArch]} runner`, () => {
    const targets = computeArchToTargetNamesMap(
      new Map([[hostArch, []]]),
      { platformSpecificBuildOptions: build.mac, defaultTarget: ['dmg'] },
      Platform.MAC,
    );
    assert.deepEqual([...targets.keys()].sort(), [Arch.x64, Arch.arm64].sort());
    assert.deepEqual(targets.get(Arch.x64), ['dmg']);
    assert.deepEqual(targets.get(Arch.arm64), ['dmg']);
  });
}
