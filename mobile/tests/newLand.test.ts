// Tests for services/newLand.ts (which of the runner's territories are new
// since the last map refresh). Run with:
//   node --experimental-strip-types --test tests/newLand.test.ts

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findNewOwnLand, type LandSummary } from '../services/newLand.ts';

const mine = (id: string, area: number): LandSummary => ({
  territoryId: id,
  areaSquareMeters: area,
  isMine: true,
});
const rival = (id: string, area: number): LandSummary => ({
  territoryId: id,
  areaSquareMeters: area,
  isMine: false,
});

test('first load only remembers land and announces nothing', () => {
  const result = findNewOwnLand(null, [mine('a', 500), rival('b', 300)]);
  assert.deepEqual(result.newIds, []);
  assert.equal(result.newAreaSquareMeters, 0);
  assert.deepEqual([...result.knownIds].sort(), ['a', 'b']);
});

test('a new territory of mine is announced with its area', () => {
  const first = findNewOwnLand(null, [mine('a', 500)]);
  const next = findNewOwnLand(first.knownIds, [mine('a', 500), mine('c', 1234.4)]);
  assert.deepEqual(next.newIds, ['c']);
  assert.equal(next.newAreaSquareMeters, 1234.4);
});

test('several new pieces in one refresh are added together', () => {
  const first = findNewOwnLand(null, []);
  const next = findNewOwnLand(first.knownIds, [mine('x', 200), mine('y', 300)]);
  assert.deepEqual(next.newIds.sort(), ['x', 'y']);
  assert.equal(next.newAreaSquareMeters, 500);
});

test("another runner's new land is not announced to me", () => {
  const first = findNewOwnLand(null, [mine('a', 500)]);
  const next = findNewOwnLand(first.knownIds, [mine('a', 500), rival('r', 900)]);
  assert.deepEqual(next.newIds, []);
  assert.equal(next.newAreaSquareMeters, 0);
});

test('land that grew or shrank (same id) is not announced again', () => {
  const first = findNewOwnLand(null, [mine('a', 500)]);
  const next = findNewOwnLand(first.knownIds, [mine('a', 800)]);
  assert.deepEqual(next.newIds, []);
});

test('land that disappears and the same id comes back is announced once more', () => {
  const first = findNewOwnLand(null, [mine('a', 500)]);
  const gone = findNewOwnLand(first.knownIds, []);
  const back = findNewOwnLand(gone.knownIds, [mine('a', 500)]);
  assert.deepEqual(back.newIds, ['a']);
});
