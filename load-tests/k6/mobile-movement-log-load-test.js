import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const androidDeviceCount = Number(__ENV.ANDROID_DEVICES || 100);
const iphoneDeviceCount = Number(__ENV.IPHONE_DEVICES || 100);
const androidVus = Number(__ENV.ANDROID_VUS || Math.min(androidDeviceCount, 50));
const iphoneVus = Number(__ENV.IPHONE_VUS || Math.min(iphoneDeviceCount, 50));
const baseUrl = (__ENV.API_BASE_URL || 'http://localhost:5216').replace(/\/$/, '');
const summaryFile = __ENV.SUMMARY_FILE
  || `artifacts/load-tests/mobile-movement-log-android-${androidDeviceCount}-iphone-${iphoneDeviceCount}-summary.json`;
const combinedLogFile = __ENV.COMBINED_LOG_FILE || summaryFile.replace(/-summary\.json$/, '.log');
const androidLogFile = __ENV.ANDROID_LOG_FILE || summaryFile.replace(/-summary\.json$/, '-android.log');
const iphoneLogFile = __ENV.IPHONE_LOG_FILE || summaryFile.replace(/-summary\.json$/, '-iphone.log');
const startLatitude = Number(__ENV.START_LATITUDE || 10.7613275);
const startLongitude = Number(__ENV.START_LONGITUDE || 106.7026730);

const commonHeaders = {
  'Content-Type': 'application/json',
  'ngrok-skip-browser-warning': 'true',
};

const androidHeaders = {
  ...commonHeaders,
  'User-Agent': 'Mozilla/5.0 (Linux; Android 14; Pixel 8 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Mobile Safari/537.36',
  'X-VKFoodArea-Virtual-Device-Platform': 'Android',
};

const iphoneHeaders = {
  ...commonHeaders,
  'User-Agent': 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  'X-VKFoodArea-Virtual-Device-Platform': 'iOS',
};

const scenarios = {};

if (androidDeviceCount > 0) {
  scenarios.android_virtual_movement_logs = {
    executor: 'shared-iterations',
    exec: 'androidMovementLogs',
    vus: Math.max(1, Math.min(androidVus, androidDeviceCount)),
    iterations: androidDeviceCount,
    maxDuration: __ENV.MAX_DURATION || '2m',
  };
}

if (iphoneDeviceCount > 0) {
  scenarios.iphone_virtual_movement_logs = {
    executor: 'shared-iterations',
    exec: 'iphoneMovementLogs',
    vus: Math.max(1, Math.min(iphoneVus, iphoneDeviceCount)),
    iterations: iphoneDeviceCount,
    maxDuration: __ENV.MAX_DURATION || '2m',
  };
}

const thresholds = {
  http_req_failed: ['rate<0.01'],
};

if (androidDeviceCount > 0) {
  thresholds.android_movement_log_success = [`count>=${androidDeviceCount}`];
  thresholds.android_movement_log_failed = ['count==0'];
}

if (iphoneDeviceCount > 0) {
  thresholds.iphone_movement_log_success = [`count>=${iphoneDeviceCount}`];
  thresholds.iphone_movement_log_failed = ['count==0'];
}

export const options = {
  scenarios,
  thresholds,
};

const androidMovementLogSuccess = new Counter('android_movement_log_success');
const androidMovementLogFailed = new Counter('android_movement_log_failed');
const iphoneMovementLogSuccess = new Counter('iphone_movement_log_success');
const iphoneMovementLogFailed = new Counter('iphone_movement_log_failed');

export function setup() {
  if (androidDeviceCount <= 0 && iphoneDeviceCount <= 0) {
    fail('Set ANDROID_DEVICES or IPHONE_DEVICES to a value greater than 0.');
  }

  const healthUrl = `${baseUrl}/api/pois`;

  for (let attempt = 1; attempt <= 30; attempt += 1) {
    const response = http.get(healthUrl, { headers: commonHeaders, timeout: '5s' });
    if (response.status === 200) {
      console.log(JSON.stringify({
        phase: 'setup',
        baseUrl,
        healthUrl,
        healthStatus: response.status,
        androidDevices: androidDeviceCount,
        iphoneDevices: iphoneDeviceCount,
        ready: true,
      }));

      return;
    }

    console.warn(`API is not ready yet: ${healthUrl} status=${response.status} attempt=${attempt}/30`);
    sleep(1);
  }

  fail(`API is not ready at ${healthUrl}. Start VKFoodArea.Web on port 5216 before running k6.`);
}

export function androidMovementLogs() {
  const deviceIndex = exec.scenario.iterationInTest + 1;
  postMovementLog({
    platform: 'Android',
    userKeyPrefix: 'android-virtual',
    deviceIndex,
    deviceCount: androidDeviceCount,
    headers: androidHeaders,
    successCounter: androidMovementLogSuccess,
    failedCounter: androidMovementLogFailed,
    coordinateShift: 0.000001,
  });
}

export function iphoneMovementLogs() {
  const deviceIndex = exec.scenario.iterationInTest + 1;
  postMovementLog({
    platform: 'iPhone/iOS',
    userKeyPrefix: 'iphone-virtual',
    deviceIndex,
    deviceCount: iphoneDeviceCount,
    headers: iphoneHeaders,
    successCounter: iphoneMovementLogSuccess,
    failedCounter: iphoneMovementLogFailed,
    coordinateShift: 0.000002,
  });
}

function postMovementLog({
  platform,
  userKeyPrefix,
  deviceIndex,
  deviceCount,
  headers,
  successCounter,
  failedCounter,
  coordinateShift,
}) {
  const userKey = `${userKeyPrefix}-${String(deviceIndex).padStart(4, '0')}`;
  const coordinateOffset = (deviceIndex % 20) * coordinateShift;

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

  if (check(response, { [`${platform} movement log status is 200`]: (r) => r.status === 200 })) {
    successCounter.add(1);
  } else {
    failedCounter.add(1);
  }

  if (deviceIndex <= 3 || deviceIndex === deviceCount) {
    console.log(JSON.stringify({
      sampleDevice: userKey,
      virtualPlatform: platform,
      status: response.status,
      rule: 'Each virtual device is one unique userKey posting one movement log row.',
    }));
  }

  sleep(0.01);
}

export function handleSummary(data) {
  const summary = {
    input: {
      baseUrl,
      androidDevices: androidDeviceCount,
      iphoneDevices: iphoneDeviceCount,
      totalVirtualDevices: androidDeviceCount + iphoneDeviceCount,
      androidVus: androidDeviceCount > 0 ? Math.max(1, Math.min(androidVus, androidDeviceCount)) : 0,
      iphoneVus: iphoneDeviceCount > 0 ? Math.max(1, Math.min(iphoneVus, iphoneDeviceCount)) : 0,
      summaryFile,
      combinedLogFile,
      androidLogFile,
      iphoneLogFile,
    },
    meaning: 'This is not physical phones or real emulators. It is a k6 API load test that simulates Android and iPhone clients with unique userKey values, mobile User-Agent headers, and movement-log payloads.',
    result: {
      totalRequests: data.metrics.http_reqs?.values?.count || 0,
      failedRequestRate: data.metrics.http_req_failed?.values?.rate || 0,
      androidMovementLogSuccess: data.metrics.android_movement_log_success?.values?.count || 0,
      androidMovementLogFailed: data.metrics.android_movement_log_failed?.values?.count || 0,
      iphoneMovementLogSuccess: data.metrics.iphone_movement_log_success?.values?.count || 0,
      iphoneMovementLogFailed: data.metrics.iphone_movement_log_failed?.values?.count || 0,
    },
  };

  const combinedLog = buildCombinedLog(summary);
  const androidLog = buildPlatformLog({
    platform: 'Android',
    expectedDevices: androidDeviceCount,
    vus: summary.input.androidVus,
    success: summary.result.androidMovementLogSuccess,
    failed: summary.result.androidMovementLogFailed,
    userKeyPrefix: 'android-virtual',
  });
  const iphoneLog = buildPlatformLog({
    platform: 'iPhone/iOS',
    expectedDevices: iphoneDeviceCount,
    vus: summary.input.iphoneVus,
    success: summary.result.iphoneMovementLogSuccess,
    failed: summary.result.iphoneMovementLogFailed,
    userKeyPrefix: 'iphone-virtual',
  });

  return {
    stdout: `${combinedLog}\n${JSON.stringify(summary, null, 2)}\n`,
    [summaryFile]: JSON.stringify(summary, null, 2),
    [combinedLogFile]: combinedLog,
    [androidLogFile]: androidLog,
    [iphoneLogFile]: iphoneLog,
  };
}

function buildCombinedLog(summary) {
  return [
    '============================================================',
    'VKFoodArea mobile movement-log k6 test',
    '============================================================',
    'Input:',
    `  Base URL              : ${summary.input.baseUrl}`,
    `  Android virtual devices: ${summary.input.androidDevices}`,
    `  iPhone virtual devices : ${summary.input.iphoneDevices}`,
    `  Total virtual devices  : ${summary.input.totalVirtualDevices}`,
    `  Android VUS            : ${summary.input.androidVus}`,
    `  iPhone VUS             : ${summary.input.iphoneVus}`,
    '',
    'Result:',
    `  Total HTTP requests     : ${summary.result.totalRequests}`,
    `  Failed request rate     : ${summary.result.failedRequestRate}`,
    `  Android success         : ${summary.result.androidMovementLogSuccess}`,
    `  Android failed          : ${summary.result.androidMovementLogFailed}`,
    `  iPhone success          : ${summary.result.iphoneMovementLogSuccess}`,
    `  iPhone failed           : ${summary.result.iphoneMovementLogFailed}`,
    `  PASS                    : ${summary.result.androidMovementLogFailed === 0 && summary.result.iphoneMovementLogFailed === 0 ? 'YES' : 'NO'}`,
    '',
    'Files:',
    `  JSON summary            : ${summary.input.summaryFile}`,
    `  Combined log            : ${summary.input.combinedLogFile}`,
    `  Android log             : ${summary.input.androidLogFile}`,
    `  iPhone log              : ${summary.input.iphoneLogFile}`,
    '',
  ].join('\n');
}

function buildPlatformLog({
  platform,
  expectedDevices,
  vus,
  success,
  failed,
  userKeyPrefix,
}) {
  return [
    '============================================================',
    `VKFoodArea ${platform} virtual device log`,
    '============================================================',
    'Input:',
    `  Platform               : ${platform}`,
    `  Expected virtual devices: ${expectedDevices}`,
    `  VUS                    : ${vus}`,
    `  UserKey prefix          : ${userKeyPrefix}`,
    '',
    'Result:',
    `  Movement log success    : ${success}`,
    `  Movement log failed     : ${failed}`,
    `  PASS                    : ${success >= expectedDevices && failed === 0 ? 'YES' : 'NO'}`,
    '',
    'Meaning:',
    '  One virtual device means one unique userKey posting one movement log row.',
    '  This is API simulation, not a physical phone or real emulator count.',
    '',
  ].join('\n');
}
