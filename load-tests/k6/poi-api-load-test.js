import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const deviceCount = Number(__ENV.DEVICES || 1000);
const defaultVus = Math.min(deviceCount, 100);
const baseUrl = (__ENV.API_BASE_URL || 'http://localhost:5216').replace(/\/$/, '');
const poiId = Number(__ENV.POI_ID || 1);
const poiName = __ENV.POI_NAME || 'Oc Oanh';
const qrCode = __ENV.QR_CODE || 'poi:oc-oanh';
const headers = {
  'Content-Type': 'application/json',
  'ngrok-skip-browser-warning': 'true',
};

export const options = {
  scenarios: {
    one_shot_virtual_devices: {
      executor: 'shared-iterations',
      vus: Number(__ENV.VUS || defaultVus),
      iterations: deviceCount,
      maxDuration: __ENV.MAX_DURATION || '2m',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    narration_success: [`count>=${deviceCount}`],
    narration_failed: ['count==0'],
  },
};

const narrationSuccess = new Counter('narration_success');
const narrationFailed = new Counter('narration_failed');
const heartbeatSuccess = new Counter('heartbeat_success');
const heartbeatFailed = new Counter('heartbeat_failed');

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
  const deviceKey = `virtual-device-${String(deviceIndex).padStart(4, '0')}`;
  const userKey = deviceKey;

  const heartbeatPayload = JSON.stringify({
    deviceKey,
    userKey,
    username: `load-user-${deviceIndex}`,
    fullName: `Load User ${deviceIndex}`,
    platform: 'k6',
    deviceName: `k6 virtual device ${deviceIndex}`,
    appVersion: 'load-test',
    isOnline: true,
  });

  const heartbeatResponse = http.post(
    `${baseUrl}/api/device-presence/heartbeat`,
    heartbeatPayload,
    { headers },
  );

  if (check(heartbeatResponse, { 'heartbeat status is 200': (r) => r.status === 200 })) {
    heartbeatSuccess.add(1);
  } else {
    heartbeatFailed.add(1);
  }

  const narrationPayload = JSON.stringify({
    poiId,
    poiName,
    qrCode,
    userKey,
    language: 'vi',
    triggerSource: 'gps',
    mode: 'tts',
    playedAt: new Date().toISOString(),
    durationSeconds: 3,
  });

  const narrationResponse = http.post(
    `${baseUrl}/api/narration-histories`,
    narrationPayload,
    { headers },
  );

  if (check(narrationResponse, { 'narration status is 201': (r) => r.status === 201 })) {
    narrationSuccess.add(1);
  } else {
    narrationFailed.add(1);
  }

  if (deviceIndex <= 3 || deviceIndex === deviceCount) {
    console.log(JSON.stringify({
      sampleDevice: deviceKey,
      poiId,
      heartbeatStatus: heartbeatResponse.status,
      narrationStatus: narrationResponse.status,
      rule: 'Virtual DeviceKey values access one POI. Server stores history rows; narration queue stays app device-local.',
    }));
  }

  sleep(0.01);
}

export function handleSummary(data) {
  const summary = {
    input: {
      baseUrl,
      poiId,
      poiName,
      qrCode,
      virtualDevices: deviceCount,
    },
    rule: 'No Android emulators. DeviceKey values post heartbeat and narration history. Narration queue is device-local in the app, not a shared server queue per POI.',
    result: {
      totalRequests: data.metrics.http_reqs?.values?.count || 0,
      failedRequestRate: data.metrics.http_req_failed?.values?.rate || 0,
      heartbeatSuccess: data.metrics.heartbeat_success?.values?.count || 0,
      heartbeatFailed: data.metrics.heartbeat_failed?.values?.count || 0,
      narrationSuccess: data.metrics.narration_success?.values?.count || 0,
      narrationFailed: data.metrics.narration_failed?.values?.count || 0,
    },
  };

  return {
    stdout: `${JSON.stringify(summary, null, 2)}\n`,
    'artifacts/load-tests/poi-api-load-test-summary.json': JSON.stringify(summary, null, 2),
  };
}
