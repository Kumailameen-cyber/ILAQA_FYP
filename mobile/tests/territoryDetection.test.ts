
import {
  detectTerritoryCandidate,
  type GpsCoordinate,
  isGpsMovementValid
} from '../services/territoryDetection';

import { detectMultipleTerritories } from '../services/territoryDetection';

// Approximate coordinates near Karachi.
// These are simulated points, not actual GPS readings.

const squareRoute: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0015 },
  { latitude: 24.8611, longitude: 67.0015 },
  { latitude: 24.8611, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0011 },
];

const straightRoute: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8608, longitude: 67.0012 },
  { latitude: 24.8609, longitude: 67.0013 },
  { latitude: 24.8610, longitude: 67.0014 },
  { latitude: 24.8611, longitude: 67.0015 },
];

const outAndBackRoute: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8608, longitude: 67.0012 },
  { latitude: 24.8609, longitude: 67.0013 },
  { latitude: 24.8608, longitude: 67.0012 },
  { latitude: 24.8607, longitude: 67.0011 },
];

function runTest(
  name: string,
  points: GpsCoordinate[],
  expectedTerritory: boolean
): void {
  const result = detectTerritoryCandidate(points);
  const detected = result !== null;

  const passed = detected === expectedTerritory;

  console.log(
    `${passed ? 'PASS' : 'FAIL'}: ${name}`
  );

  if (result) {
    console.log(
      `Area: ${result.areaSquareMeters.toFixed(2)} m²`
    );
  }

  if (!passed) {
    throw new Error(`Test failed: ${name}`);
  }
}

runTest('Square loop', squareRoute, true);
runTest('Straight route', straightRoute, false);
runTest('Out and back', outAndBackRoute, false);

const crossedRoute: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8611, longitude: 67.0015 },
  { latitude: 24.8611, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0015 },
  { latitude: 24.8607, longitude: 67.0011 },
];

runTest(
  'Self-intersecting route',
  crossedRoute,
  false
);

function runMultipleTerritoryTest(
  name: string,
  points: GpsCoordinate[],
  expectedCount: number
) {
  const territories = detectMultipleTerritories(points);

  const passed = territories.length === expectedCount;

  console.log(
    `${passed ? 'PASS' : 'FAIL'}: ${name} | Expected: ${expectedCount}, Found: ${territories.length}`
  );

  if (!passed) {
    throw new Error(`${name} failed`);
  }
}

// Test 1: Two blocks sharing a junction
const twoAdjacentBlocks: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0015 },
  { latitude: 24.8611, longitude: 67.0015 },
  { latitude: 24.8611, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0011 },

  { latitude: 24.8607, longitude: 67.0007 },
  { latitude: 24.8611, longitude: 67.0007 },
  { latitude: 24.8611, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0011 },
];

// Test 2: Two separate blocks connected by a road
const twoSeparateBlocks: GpsCoordinate[] = [
  ...squareRoute,

  // Travel to another block
  { latitude: 24.8607, longitude: 67.0020 },

  // Run around the second block
  { latitude: 24.8607, longitude: 67.0024 },
  { latitude: 24.8611, longitude: 67.0024 },
  { latitude: 24.8611, longitude: 67.0020 },
  { latitude: 24.8607, longitude: 67.0020 },
];

// Test 3: Straight route must not create territory
runMultipleTerritoryTest(
  'Straight route',
  straightRoute,
  0
);

// Test 4: Out and back must not create territory
runMultipleTerritoryTest(
  'Out and back',
  outAndBackRoute,
  0
);

// Test 5: Two adjacent blocks
runMultipleTerritoryTest(
  'Two adjacent blocks',
  twoAdjacentBlocks,
  2
);

// Test 6: Two separate blocks
runMultipleTerritoryTest(
  'Two separate blocks',
  twoSeparateBlocks,
  2
);

console.log('Multiple territory tests completed!');

console.log('All territory tests passed!');

import { detectTerritoriesByLeg, LegGpsPoint } from '../services/territoryDetection';

const pausedRoute: LegGpsPoint[] = [
  // Leg 1: Only half of a square
  { latitude: 24.8607, longitude: 67.0011, legNo: 1 },
  { latitude: 24.8607, longitude: 67.0015, legNo: 1 },
  { latitude: 24.8611, longitude: 67.0015, legNo: 1 },

  // Runner pauses, then resumes
  // Leg 2: Remaining half of the square
  { latitude: 24.8611, longitude: 67.0011, legNo: 2 },
  { latitude: 24.8607, longitude: 67.0011, legNo: 2 },
];

const pausedTerritories = detectTerritoriesByLeg(pausedRoute);

if (pausedTerritories.length !== 0) {
  throw new Error(
    'FAIL: Pause/Resume created a fake territory!'
  );
}

console.log('PASS: Pause/Resume does not create fake territory');

// A complete loop inside one leg should still work.
const validLegRoute: LegGpsPoint[] = squareRoute.map(
  (point) => ({
    ...point,
    legNo: 1,
  })
);

const validLegTerritories =
  detectTerritoriesByLeg(validLegRoute);

if (validLegTerritories.length !== 1) {
  throw new Error(
    'FAIL: Valid territory inside one leg was not detected!'
  );
}

console.log('PASS: Valid territory within one leg');

const noisySquareRoute: GpsCoordinate[] = [
  { latitude: 24.860700, longitude: 67.001100 },
  { latitude: 24.860705, longitude: 67.001300 },
  { latitude: 24.860700, longitude: 67.001500 },

  { latitude: 24.860900, longitude: 67.001505 },
  { latitude: 24.861100, longitude: 67.001500 },

  { latitude: 24.861105, longitude: 67.001300 },
  { latitude: 24.861100, longitude: 67.001100 },

  { latitude: 24.860900, longitude: 67.001095 },
  { latitude: 24.860706, longitude: 67.001104 },
];

runMultipleTerritoryTest(
  'Noisy GPS square',
  noisySquareRoute,
  1
);

const gpsJumpRoute: GpsCoordinate[] = [
  { latitude: 24.8607, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0021 },
  { latitude: 24.8617, longitude: 67.0021 },
  { latitude: 24.8617, longitude: 67.0011 },
  { latitude: 24.8607, longitude: 67.0011 },
];

// runMultipleTerritoryTest(
//   'GPS jump creates fake square',
//   gpsJumpRoute,
//   0
// );

const timestampedGpsJumpRoute = [
  {
    latitude: 24.8607,
    longitude: 67.0011,
    legNo: 0,
    timestamp: 1000000,
    accuracy: 5,
  },
  {
    latitude: 24.8607,
    longitude: 67.0021,
    legNo: 0,
    timestamp: 1001000,
    accuracy: 5,
  },
  {
    latitude: 24.8617,
    longitude: 67.0021,
    legNo: 0,
    timestamp: 1002000,
    accuracy: 5,
  },
  {
    latitude: 24.8617,
    longitude: 67.0011,
    legNo: 0,
    timestamp: 1003000,
    accuracy: 5,
  },
  {
    latitude: 24.8607,
    longitude: 67.0011,
    legNo: 0,
    timestamp: 1004000,
    accuracy: 5,
  },
];

const fakeTerritories = detectTerritoriesByLeg(
  timestampedGpsJumpRoute
);

if (fakeTerritories.length !== 0) {
  throw new Error(
    `GPS jump test failed: expected 0 territories, found ${fakeTerritories.length}`
  );
}

console.log(
  'PASS: Timestamped GPS jumps cannot create fake territories'
);

const recoverableGpsRoute = [
  { latitude: 24.8607, longitude: 67.0011, legNo: 1, timestamp: 1000000, accuracy: 5 },
  { latitude: 24.8607, longitude: 67.0013, legNo: 1, timestamp: 1010000, accuracy: 5 },

  // One incorrect GPS reading, far from the real route.
  { latitude: 24.8650, longitude: 67.0100, legNo: 1, timestamp: 1015000, accuracy: 5 },

  { latitude: 24.8607, longitude: 67.0015, legNo: 1, timestamp: 1020000, accuracy: 5 },
  { latitude: 24.8609, longitude: 67.0015, legNo: 1, timestamp: 1030000, accuracy: 5 },
  { latitude: 24.8611, longitude: 67.0015, legNo: 1, timestamp: 1040000, accuracy: 5 },
  { latitude: 24.8611, longitude: 67.0011, legNo: 1, timestamp: 1060000, accuracy: 5 },
  { latitude: 24.8607, longitude: 67.0011, legNo: 1, timestamp: 1080000, accuracy: 5 },
];

const recoveredTerritories =
  detectTerritoriesByLeg(recoverableGpsRoute);

if (recoveredTerritories.length !== 1) {
  throw new Error(
    `GPS recovery failed: expected 1 territory, found ${recoveredTerritories.length}`
  );
}

console.log('PASS: Genuine territory survives isolated GPS error');

function testGpsMovementValidation() {
  const start = {
    latitude: 24.8607,
    longitude: 67.0011,
    legNo: 0,
    timestamp: 1000000,
    accuracy: 5,
  };

  // Test 1: Normal running movement
  const normalMovement = {
    ...start,
    latitude: 24.860745,
    timestamp: 1002000,
  };

  if (!isGpsMovementValid(start, normalMovement)) {
    throw new Error('Normal GPS movement should be valid');
  }

  console.log('PASS: Normal GPS movement');

  // Test 2: Impossible GPS jump
  const gpsJump = {
    ...start,
    latitude: 24.8617,
    timestamp: 1001000,
  };

  if (isGpsMovementValid(start, gpsJump)) {
    throw new Error('GPS jump should be rejected');
  }

  console.log('PASS: Impossible GPS jump rejected');

  // Test 3: Poor GPS accuracy
  const poorAccuracy = {
    ...normalMovement,
    accuracy: 70,
  };

  if (isGpsMovementValid(start, poorAccuracy)) {
    throw new Error('Poor GPS accuracy should be rejected');
  }

  console.log('PASS: Poor GPS accuracy rejected');

  // Test 4: Different running legs
  const differentLeg = {
    ...normalMovement,
    legNo: 1,
  };

  if (isGpsMovementValid(start, differentLeg)) {
    throw new Error('Different legs should not be connected');
  }

  console.log('PASS: Different running legs rejected');

  // Test 5: Missing timestamps
  const missingTime = {
    ...normalMovement,
    timestamp: undefined,
  };

  if (isGpsMovementValid(start, missingTime)) {
    throw new Error('Missing timestamps should be rejected');
  }

  console.log('PASS: Missing timestamp rejected');
}

testGpsMovementValidation();
