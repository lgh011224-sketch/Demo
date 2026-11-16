// 하우징 센서 + 진동 스케치 (두더지 잡기 · 사격장 공용)
// 센서: GY-6500 / GY-9250 보드 (MPU-6500 / MPU-9250 칩, 가속도 + 자이로). 라이브러리 없이 I2C로 직접 읽습니다.
//
// ── 배선 (아두이노 UNO / Nano 기준) ──────────────────────────────
//   센서 VCC → 5V   (GY 보드에는 3.3V 레귤레이터가 있음. 보드에 레귤레이터가 없으면 3.3V에 연결)
//   센서 GND → GND
//   센서 SDA → A4   (Mega는 20번)
//   센서 SCL → A5   (Mega는 21번)
//   센서 AD0 → 연결 안 함 (주소 0x68). AD0를 VCC에 연결했다면 아래 MPU_ADDR을 0x69로
//   진동 모터 → 트랜지스터/모터 드라이버 → 9번 핀 (모터를 핀에 직접 연결하면 안 됨)
//   (선택) 발사 버튼 → 2번 핀과 GND 사이,  가운데 맞추기 버튼 → 3번 핀과 GND 사이
//
// ── Unity로 보내는 것 (115200bps, 한 줄씩) ───────────────────────
//   I <dt μs> <ax> <ay> <az> <gx> <gy> <gz>   약 200번/초. 가속도 ±4g(8192/g), 자이로 ±1000°/s(32.8/°/s)
//   FIRE      발사 버튼을 눌렀을 때
//   CENTER    가운데 맞추기 버튼을 눌렀을 때
//   IMU OK 0x71 / IMU FAIL ...   시작할 때 센서 확인 결과
// ── Unity에서 받는 것 ────────────────────────────────────────────
//   VIB <세기 0~255> <시간 ms>   진동
//   MODE STICK / MODE HAMMER     하우징 형태 변경 (필요하면 handleLine에 동작 추가)
//
// 센서를 하우징에 붙일 때: 보드의 X 화살표가 손잡이 → 머리(총구) 방향을 향하게 붙이면
// Unity 설정을 바꿀 필요가 없습니다. 다른 방향이면 Unity의 ImuInput > Forward Axis 를 바꾸세요.
// 처음 켜면 약 1.5초 동안 자이로 영점을 잡으니 하우징을 가만히 두세요.
#include <Wire.h>

const uint8_t MPU_ADDR = 0x68;
const int MOTOR_PIN = 9;
const int FIRE_PIN = 2;
const int CENTER_PIN = 3;
const unsigned long SAMPLE_US = 5000;   // 200 Hz

unsigned long vibEndMs = 0;
unsigned long lastSample = 0;
String line;
bool imuOk = false;
bool lastFire = HIGH, lastCenter = HIGH;
unsigned long fireDebounce = 0, centerDebounce = 0;
int readFails = 0;                     // 연속 읽기 실패 수 (많으면 센서 다시 설정)

void writeReg(uint8_t reg, uint8_t val) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(reg);
  Wire.write(val);
  Wire.endTransmission();
}

uint8_t readReg(uint8_t reg) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(reg);
  Wire.endTransmission(false);
  Wire.requestFrom(MPU_ADDR, (uint8_t)1);
  return Wire.available() ? Wire.read() : 0xFF;
}

// I2C 선 풀어 주기: 통신 도중 선이 흔들려 끊기면 센서가 SDA를 LOW로 붙잡고 멈출 수 있음
// → SCL을 최대 9번 흔들어 센서가 하던 전송을 끝내게 한 뒤 I2C를 다시 시작
void startI2C() {
  Wire.end();
  pinMode(A4, INPUT_PULLUP);
  pinMode(A5, INPUT_PULLUP);
  delayMicroseconds(20);
  for (int i = 0; i < 9 && digitalRead(A4) == LOW; i++) {
    pinMode(A5, OUTPUT); digitalWrite(A5, LOW); delayMicroseconds(10);
    pinMode(A5, INPUT_PULLUP); delayMicroseconds(10);
  }
  Wire.begin();
  Wire.setClock(100000);             // 칼 끝까지 긴 선이라 느린 속도(100 kHz)가 더 안정적
  Wire.setWireTimeout(3000, true);   // 선이 잠깐 끊겨도 멈추지 않고 3ms 뒤 I2C를 재설정
}

void setupImu() {
  startI2C();
  writeReg(0x6B, 0x80);   // PWR_MGMT_1: 리셋
  delay(100);
  writeReg(0x6B, 0x01);   // 자이로 클럭 사용
  delay(10);
  writeReg(0x1A, 0x03);   // CONFIG: 자이로 저역 필터 41 Hz
  writeReg(0x19, 0x04);   // SMPLRT_DIV: 1 kHz / 5 = 200 Hz
  writeReg(0x1B, 0x10);   // GYRO_CONFIG: ±1000 °/s
  writeReg(0x1C, 0x08);   // ACCEL_CONFIG: ±4 g
  writeReg(0x1D, 0x03);   // ACCEL_CONFIG2: 가속도 저역 필터 44 Hz
  uint8_t who = readReg(0x75);
  // 0x70 = MPU-6500, 0x71 = MPU-9250, 0x73 = MPU-9255, 0x68 = MPU-6050
  imuOk = (who == 0x70 || who == 0x71 || who == 0x73 || who == 0x68);
  Serial.print(imuOk ? "IMU OK 0x" : "IMU FAIL who=0x");
  Serial.println(who, HEX);
}

void setup() {
  pinMode(MOTOR_PIN, OUTPUT);
  analogWrite(MOTOR_PIN, 0);
  pinMode(FIRE_PIN, INPUT_PULLUP);
  pinMode(CENTER_PIN, INPUT_PULLUP);
  Serial.begin(115200);
  setupImu();                        // I2C 시작(선 풀어 주기 포함) + 센서 설정
  lastSample = micros();
}

void handleLine(String s) {
  s.trim();
  if (s.startsWith("VIB")) {
    int a = s.indexOf(' ');
    int b = s.indexOf(' ', a + 1);
    if (a < 0 || b < 0) return;
    int pwm = constrain(s.substring(a + 1, b).toInt(), 0, 255);
    long ms = max(1L, s.substring(b + 1).toInt());
    analogWrite(MOTOR_PIN, pwm);
    vibEndMs = millis() + ms;
  } else if (s == "IMU?") {
    setupImu();
  }
  // MODE STICK / MODE HAMMER: 하우징 형태 변경 장치가 있으면 여기서 처리
}

void sendSample() {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);                       // ACCEL_XOUT_H 부터 14바이트
  if (Wire.endTransmission(false) != 0 || Wire.requestFrom(MPU_ADDR, (uint8_t)14) != 14) {
    lastSample = micros();
    if (++readFails >= 20) { Serial.println("IMU LOST - 다시 설정"); readFails = 0; setupImu(); }
    return;
  }
  readFails = 0;
  int16_t v[7];
  for (int i = 0; i < 7; i++) v[i] = (int16_t)((Wire.read() << 8) | Wire.read());
  unsigned long now = micros();
  unsigned long dt = now - lastSample;
  lastSample = now;
  // v[3] = 온도 (보내지 않음)
  Serial.print("I "); Serial.print(dt);
  Serial.print(' '); Serial.print(v[0]); Serial.print(' '); Serial.print(v[1]); Serial.print(' '); Serial.print(v[2]);
  Serial.print(' '); Serial.print(v[4]); Serial.print(' '); Serial.print(v[5]); Serial.print(' '); Serial.println(v[6]);
}

void checkButton(int pin, bool &last, unsigned long &deb, const char *msg) {
  bool now = digitalRead(pin);
  if (now != last && millis() - deb > 30) {
    deb = millis();
    last = now;
    if (now == LOW) Serial.println(msg);
  }
}

void loop() {
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') { handleLine(line); line = ""; }
    else if (c != '\r') line += c;
  }
  if (vibEndMs != 0 && (long)(millis() - vibEndMs) >= 0) {
    analogWrite(MOTOR_PIN, 0);
    vibEndMs = 0;
  }
  if (imuOk && micros() - lastSample >= SAMPLE_US) sendSample();
  else if (!imuOk && millis() % 2000 == 0) { delay(1); setupImu(); }   // 센서가 안 잡혔으면 2초마다 다시 시도
  checkButton(FIRE_PIN, lastFire, fireDebounce, "FIRE");
  checkButton(CENTER_PIN, lastCenter, centerDebounce, "CENTER");
}
