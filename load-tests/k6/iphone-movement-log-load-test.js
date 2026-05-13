import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const deviceCount = Number(__ENV.DEVICES || 100);
const defaultVus = Math.min(deviceCount, 50);
const baseUrl = (__ENV.API_BASE_URL || 'http://localhost:5216').replace(/\/$/, '');
const summaryFile = __ENV.SUMMARY_FILE || `artifacts/load-tests/iphone-movement-log-${deviceCount}-summary.json`;
const startLatitude = Number(__ENV.START_LATITUDE || 10.7613275);
const startLongitude = Number(__ENV.START_LONGITUDE || 106.7026730);

const headers = {
  'Content-Type': 'application/json',
  'ngrok-skip-browser-warning': 'true',
  'User-Agent': 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  'X-VKFoodArea-Virtual-Device-Platform': 'iOS',
};

export const options = {
  scenarios: {
    iphone_virtual_movement_logs: {
      executor: 'shared-iterations',
      vus: Number(__ENV.VUS || defaultVus),
      iterations: deviceCount,
      maxDuration: __ENV.MAX_DURATION || '2m',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    movement_log_success: [`count>=${deviceCount}`],
    movement_log_failed: ['count==0'],
  },
};

const movementLogSuccess = new Counter('movement_log_success');
const movementLogFailed = new Counter('movement_log_failed');

export function setup() {
  const healthUrl = `${baseUrl}/api/pois`;

  for (let attempt = 1; attempt <= 30; attempt += 1) {
    const response = http.get(healthUrl, { headers, timeout: '5s' });
    if (response.status === 200) {
      console.log(JSON.stringify({
        phase: 'setup',
        baseUrl,
        healthUrl,
        healthStatus: response.status,
        virtualPlatform: 'iPhone/iOS API simulation',
        ready: true,
      }));

      return;
    }

    console.warn(`API is not ready yet: ${healthUrl} status=${response.status} attempt=${attempt}/30`);
    sleep(1);
  }

  fail(`API is not ready at ${healthUrl}. Start VKFoodArea.Web on port 5216 before running k6.`);
}

export default function () {
  const deviceIndex = exec.scenario.iterationInTest + 1;
  const userKey = `iphone-virtual-${String(deviceIndex).padStart(4, '0')}`;
  const coordinateOffset = (deviceIndex % 20) * 0.000001;

  const payload = JSON.stringify({
    userKey,
    latitude: startLatitude + coordinateOffset,
    longitude: startLongitude + coordinateOffset,
    accuracyMeters: 8 + (deviceIndex % 5),
    source: 'gps',
    recordedAt: new Date().toISOString(),
  });

  const response = http.post(
    `${baseUrl}/api/movement-logs`,
    payload,
    { headers },
  );

  if (check(response, { 'movement log status is 200': (r) => r.status === 200 })) {
    movementLogSuccess.add(1);
  } else {
    movementLogFailed.add(1);
  }

  if (deviceIndex <= 3 || deviceIndex === deviceCount) {
    console.log(JSON.stringify({
      sampleDevice: userKey,
      virtualPlatform: 'iPhone/iOS',
      status: response.status,
      rule: 'Each virtual iPhone is one unique userKey posting one movement log row.',
    }));
  }

  sleep(0.01);
}

export function handleSummary(data) {
  const summary = {
    input: {
      baseUrl,
      virtualPlatform: 'iPhone/iOS API simulation',
      virtualDevices: deviceCount,
      vus: Number(__ENV.VUS || defaultVus),
      summaryFile,
    },
    meaning: 'This is not 100 physical iPhones and not 100 iOS Simulators. It is a k6 API load test that simulates iPhone clients by unique userKey values, iPhone User-Agent headers, and movement-log payloads.',
    result: {
      totalRequests: data.metrics.http_reqs?.values?.count || 0,
      failedRequestRate: data.metrics.http_req_failed?.values?.rate || 0,
      movementLogSuccess: data.metrics.movement_log_success?.values?.count || 0,
      movementLogFailed: data.metrics.movement_log_failed?.values?.count || 0,
    },
  };

  return {
    stdout: `${JSON.stringify(summary, null, 2)}\n`,
    [summaryFile]: JSON.stringify(summary, null, 2),
  };
}
