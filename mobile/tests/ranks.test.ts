// Tests for services/ranks.ts (how ranking values are written on screen).
// Run with: node --experimental-strip-types --test tests/ranks.test.ts

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatRankValue, formatActiveTime, formatRank } from '../services/ranks.ts';

test('area under 1 km² is shown in m² with thousands commas', () => {
  assert.equal(formatRankValue('area', 0), '0 m²');
  assert.equal(formatRankValue('area', 1234.6), '1,235 m²');
  assert.equal(formatRankValue('area', 999999), '999,999 m²');
});

test('area of 1 km² or more is shown in km²', () => {
  assert.equal(formatRankValue('area', 1_000_000), '1.00 km²');
  assert.equal(formatRankValue('area', 2_345_678), '2.35 km²');
});

test('distance under 1 km is in metres, otherwise km', () => {
  assert.equal(formatRankValue('distance', 850.4), '850 m');
  assert.equal(formatRankValue('distance', 3421), '3.42 km');
});

test('territories are a plain count', () => {
  assert.equal(formatRankValue('territories', 1), '1');
  assert.equal(formatRankValue('territories', 12), '12');
});

test('active time is minutes, or hours and minutes', () => {
  assert.equal(formatActiveTime(0), '0 min');
  assert.equal(formatActiveTime(59), '0 min');
  assert.equal(formatActiveTime(1800), '30 min');
  assert.equal(formatActiveTime(3600 * 5 + 60 * 12), '5 h 12 min');
});

test('a missing rank shows a dash', () => {
  assert.equal(formatRank(null), '—');
  assert.equal(formatRank(undefined), '—');
  assert.equal(formatRank(3), '#3');
});
