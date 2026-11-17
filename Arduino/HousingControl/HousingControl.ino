// ===== 하우징 제어 (Unity 연동용) =====
// 115200bps. Unity(ArduinoSerial) 또는 시리얼 모니터에서 한 줄씩 명령을 받습니다.
//
//  1 / UP   / MODE HAMMER : 스테퍼를 맨 위로   (1단계 징그러운 두더지 · 쇠 망치)
//  2 / DOWN / MODE STICK  : 스테퍼를 맨 아래로 (2단계 귀여운 두더지 · 드럼 스틱)
//  3 / GUN  / MODE GUN    : 스테퍼를 맨 아래로 내린 뒤 스크류를 SCREW_GUN_MS 만큼 밀기 (사격 씬)
//                           (이미 밀어 둔 상태면 다시 밀지 않음)
//  4 / PUSH [ms]          : 스크류 밀기   (ms 생략 시 500)
//  5 / PULL [ms]          : 스크류 당기기 (ms 생략 시 500, 당기면 '밀어 둔 상태' 해제)
//  6 / 7                  : 스테퍼 위/아래로 1000스텝 조금씩 (위치 기억은 안 바뀜 = 영점 맞추기용)
//  HOME                   : 처음 상태로 (스테퍼 맨 아래 + 밀어 둔 스크류가 있으면 같은 시간만큼 당기기)
//                           Unity를 끌 때 자동 전송 → 다음 실행 때 '맨 아래·스크류 들어감' 가정이 맞도록
//  Z                      : 지금 위치를 맨 아래(0)로 기억
//  0 / STOP               : 모두 정지
//
// 위치: 전원을 켜거나 재시작할 때 스테퍼가 맨 아래에 있다고 가정합니다 (위치 0).
//       맨 위 = STEPPER_TRAVEL 스텝. 이미 그 위치에 있으면 움직이지 않습니다.
// 버튼: 누르면 "# BUTTON PRESSED", 떼면 "# BUTTON RELEASED" (Unity 사격 판정에 사용)

// ===== 핀 =====
const int M1_DIR  = 2;
const int M1_STEP = 3;
const int M1_EN   = 8;
const int M3_IN1  = 5;
const int M3_IN2  = 6;
const int BTN_PIN = 7;

// ===== 설정 =====
const unsigned long M1_HALF_US = 400;   // 스테퍼 속도 (작을수록 빠름)
const bool UP_DIR_LEVEL = HIGH;         // 위로 갈 때 DIR 핀 (시험 스케치의 '1' = HIGH = 위)
const long STEPPER_TRAVEL = 7000;       // 맨 아래 ↔ 맨 위 스텝 수 (실제 기구에 맞게 조절)
const long JOG_STEPS = 1000;            // 6·7번 조금씩 움직이는 스텝 수
const int M3_SPEED = 255;               // 스크류 속도
const unsigned long SCREW_DEFAULT_MS = 8300;
const unsigned long SCREW_GUN_MS = 8300; // 사격 씬에서 스크류를 미는 양 (ms)

// ===== 상태 =====
long m1Pos = 0;             // 현재 위치 (0 = 맨 아래)
long m1Left = 0;            // 남은 스텝
int  m1Dir = 0;             // +1 위, -1 아래
bool m1Track = true;        // 위치 기억에 반영할지 (조금씩 움직이기는 반영 안 함)
unsigned long m1LastUs = 0;
bool m1Level = LOW;

bool m3On = false;
unsigned long m3EndMs = 0;
bool screwPushed = false;   // 사격용으로 밀어 둔 상태
unsigned long pendingPushMs = 0;  // 스테퍼가 멈추면 시작할 스크류 밀기

bool btnLast = HIGH;
unsigned long btnChangeMs = 0;
bool btnPressed = false;

String lineBuf;

void stepperMove(long steps, bool track) {   // steps > 0 위, < 0 아래
  if (steps == 0) return;
  m1Dir = steps > 0 ? 1 : -1;
  m1Left = steps > 0 ? steps : -steps;
  m1Track = track;
  digitalWrite(M1_DIR, steps > 0 ? UP_DIR_LEVEL : !UP_DIR_LEVEL);
  digitalWrite(M1_EN, LOW);
}

void stepperGoTo(long target) {
  long d = target - m1Pos;
  if (d == 0 && m1Left == 0) {
    Serial.print("# STEPPER ALREADY AT "); Serial.println(m1Pos);
    return;
  }
  stepperMove(d, true);
  Serial.print("# STEPPER GOTO "); Serial.print(target);
  Serial.print(" (from "); Serial.print(m1Pos); Serial.println(")");
}

void screwRun(bool push, unsigned long ms) {
  if (push) { analogWrite(M3_IN2, 0); analogWrite(M3_IN1, M3_SPEED); }
  else      { analogWrite(M3_IN1, 0); analogWrite(M3_IN2, M3_SPEED); }
  m3On = true;
  m3EndMs = millis() + ms;
  Serial.print(push ? "# SCREW PUSH " : "# SCREW PULL "); Serial.println(ms);
}

void stopAll() {
  m1Left = 0;
  digitalWrite(M1_STEP, LOW);
  digitalWrite(M1_EN, HIGH);
  m3On = false;
  pendingPushMs = 0;
  analogWrite(M3_IN1, 0);
  analogWrite(M3_IN2, 0);
}

unsigned long argMs(const String& cmd, unsigned long def) {
  int sp = cmd.indexOf(' ');
  if (sp < 0) return def;
  long v = cmd.substring(sp + 1).toInt();
  return v > 0 ? (unsigned long)v : def;
}

void handle(String cmd) {
  cmd.trim();
  cmd.toUpperCase();
  if (cmd.length() == 0) return;

  if (cmd == "1" || cmd == "UP" || cmd == "MODE HAMMER") {
    pendingPushMs = 0;
    stepperGoTo(STEPPER_TRAVEL);
  } else if (cmd == "2" || cmd == "DOWN" || cmd == "MODE STICK") {
    pendingPushMs = 0;
    stepperGoTo(0);
  } else if (cmd == "3" || cmd == "GUN" || cmd == "MODE GUN") {
    stepperGoTo(0);
    if (screwPushed) Serial.println("# SCREW ALREADY PUSHED");
    else pendingPushMs = SCREW_GUN_MS;   // 스테퍼가 다 내려간 뒤 시작
  } else if (cmd == "HOME") {
    pendingPushMs = 0;
    stepperGoTo(0);
    if (screwPushed) { screwRun(false, SCREW_GUN_MS); screwPushed = false; }
  } else if (cmd == "4" || cmd.startsWith("PUSH")) {
    screwRun(true, argMs(cmd, SCREW_DEFAULT_MS));
  } else if (cmd == "5" || cmd.startsWith("PULL")) {
    screwRun(false, argMs(cmd, SCREW_DEFAULT_MS));
    screwPushed = false;
  } else if (cmd == "6") {
    stepperMove(JOG_STEPS, false);
    Serial.println("# JOG UP");
  } else if (cmd == "7") {
    stepperMove(-JOG_STEPS, false);
    Serial.println("# JOG DOWN");
  } else if (cmd == "Z") {
    m1Pos = 0;
    Serial.println("# ZERO SET (bottom)");
  } else if (cmd == "0" || cmd == "STOP") {
    stopAll();
    Serial.println("# STOP");
  } else {
    Serial.print("# ? "); Serial.println(cmd);
  }
}

void setup() {
  pinMode(M1_DIR, OUTPUT);
  pinMode(M1_STEP, OUTPUT);
  pinMode(M1_EN, OUTPUT);
  pinMode(M3_IN1, OUTPUT);
  pinMode(M3_IN2, OUTPUT);
  pinMode(BTN_PIN, INPUT_PULLUP);
  stopAll();

  Serial.begin(115200);
  Serial.println("# READY");
  Serial.println("# 1=UP 2=DOWN 3=GUN 4/5=screw push/pull 6/7=jog Z=zero 0=stop");
}

void loop() {
  unsigned long nowMs = millis();
  unsigned long nowUs = micros();

  // ----- 명령 받기 (한 줄 단위, 줄바꿈 없이 숫자 하나만 와도 처리) -----
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') { handle(lineBuf); lineBuf = ""; }
    else if (lineBuf.length() < 32) lineBuf += c;
  }
  if (lineBuf.length() == 1 && !Serial.available()) {
    // 시리얼 모니터에서 'No line ending'으로 숫자 하나만 보낸 경우
    delay(5);
    if (!Serial.available()) { handle(lineBuf); lineBuf = ""; }
  }

  // ----- 스테퍼 -----
  if (m1Left > 0 && nowUs - m1LastUs >= M1_HALF_US) {
    m1LastUs = nowUs;
    m1Level = !m1Level;
    digitalWrite(M1_STEP, m1Level);
    if (m1Level == LOW) {
      m1Left--;
      if (m1Track) m1Pos += m1Dir;
      if (m1Left == 0) {
        digitalWrite(M1_EN, HIGH);
        Serial.print("# STEPPER DONE "); Serial.println(m1Pos);
      }
    }
  }

  // ----- 사격 준비: 스테퍼가 다 내려가면 스크류 밀기 -----
  if (pendingPushMs > 0 && m1Left == 0 && !m3On) {
    screwRun(true, pendingPushMs);
    pendingPushMs = 0;
    screwPushed = true;
  }

  // ----- 스크류 -----
  if (m3On && (long)(nowMs - m3EndMs) >= 0) {
    m3On = false;
    analogWrite(M3_IN1, 0);
    analogWrite(M3_IN2, 0);
    Serial.println("# SCREW STOP");
  }

  // ----- 버튼 -----
  bool r = digitalRead(BTN_PIN);
  if (r != btnLast) { btnLast = r; btnChangeMs = nowMs; }
  if (nowMs - btnChangeMs >= 20 && (r == LOW) != btnPressed) {
    btnPressed = (r == LOW);
    Serial.println(btnPressed ? "# BUTTON PRESSED" : "# BUTTON RELEASED");
  }
}
