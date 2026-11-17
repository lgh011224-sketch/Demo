// ===== 전체 구동 확인용 간단 코드 =====
// 시리얼 모니터(115200)에 숫자 하나를 입력해서 부품을 하나씩 시험합니다.
// 1: 스테퍼 정방향 6000스텝   2: 스테퍼 역방향 6000스텝
// 6: 스테퍼 정방향 1000스텝   7: 스테퍼 역방향 1000스텝
// 4: 스크류 밀기     5: 스크류 당기기
// 0: 모두 정지
// 버튼은 누르고 뗄 때마다 자동으로 출력됩니다.
// (하우징에는 진동 장치가 없음 – 진동은 Quest 컨트롤러가 담당)

// ===== 핀 =====
const int M1_DIR  = 2;
const int M1_STEP = 3;
const int M1_EN   = 8;
const int M3_IN1  = 5;
const int M3_IN2  = 6;
const int BTN_PIN = 7;

// ===== 속도 =====
const unsigned long M1_HALF_US = 300;  // 스테퍼 속도 (작을수록 빠름)
const int M3_SPEED  = 255;             // 스크류 속도

// ===== 시험 동작 크기 =====
const long M1_LONG_STEPS  = 6000;      // 1·2번: 한 번에 움직이는 스텝 수
const long M1_SHORT_STEPS = 1000;      // 6·7번: 한 번에 움직이는 스텝 수
const unsigned long M3_TEST_MS  = 8000; // 스크류가 한 번에 도는 시간 (ms) - 사격장 진입 시 8000ms 밀기

// ===== 상태 =====
long m1Left = 0;            // 스테퍼 남은 스텝
unsigned long m1LastUs = 0;
bool m1Level = LOW;

bool m3On = false;
unsigned long m3EndMs = 0;

bool btnLast = HIGH;
unsigned long btnChangeMs = 0;
bool btnPressed = false;

void stopAll() {
  m1Left = 0;
  digitalWrite(M1_STEP, LOW);
  digitalWrite(M1_EN, HIGH);
  m3On = false;
  analogWrite(M3_IN1, 0);
  analogWrite(M3_IN2, 0);
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
  Serial.println("# 1/2: stepper 6000 fwd/rev, 6/7: stepper 1000 fwd/rev");
  Serial.println("# 4/5: screw push/pull, 0: stop");
}

void loop() {
  unsigned long nowMs = millis();
  unsigned long nowUs = micros();

  // ----- 명령 받기 -----
  if (Serial.available()) {
    char c = Serial.read();
    if (c == '1' || c == '2' || c == '6' || c == '7') {
      bool fwd = (c == '1' || c == '6');
      digitalWrite(M1_DIR, fwd ? HIGH : LOW);
      digitalWrite(M1_EN, LOW);
      m1Left = (c == '1' || c == '2') ? M1_LONG_STEPS : M1_SHORT_STEPS;
      Serial.print(fwd ? "# STEPPER FWD " : "# STEPPER REV ");
      Serial.println(m1Left);
    }
    if (c == '4' || c == '5') {
      if (c == '4') { analogWrite(M3_IN2, 0); analogWrite(M3_IN1, M3_SPEED); }
      else          { analogWrite(M3_IN1, 0); analogWrite(M3_IN2, M3_SPEED); }
      m3On = true;
      m3EndMs = nowMs + M3_TEST_MS;
      Serial.println(c == '4' ? "# SCREW PUSH" : "# SCREW PULL");
    }
    if (c == '0') {
      stopAll();
      Serial.println("# STOP");
    }
  }

  // ----- 스테퍼 -----
  if (m1Left > 0 && nowUs - m1LastUs >= M1_HALF_US) {
    m1LastUs = nowUs;
    m1Level = !m1Level;
    digitalWrite(M1_STEP, m1Level);
    if (m1Level == LOW) {
      m1Left--;
      if (m1Left == 0) {
        digitalWrite(M1_EN, HIGH);
        Serial.println("# STEPPER DONE");
      }
    }
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
