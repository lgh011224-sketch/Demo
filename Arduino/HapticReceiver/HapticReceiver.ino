// Unity(두더지 잡기·사격장) → 아두이노 진동 수신 예제
// Unity가 USB 시리얼(9600bps)로 한 줄씩 보냅니다:  "VIB <세기 0~255> <시간 ms>"
//   예) VIB 128 80   귀여운 두더지 맞힘 (약하게)
//       VIB 255 80   초록 두더지 맞힘 (최대)
//       VIB 255 70   권총 발사 반동
//       VIB 38 200   틀린 도구 (약하게 길게)
// 진동 모터는 트랜지스터/모터 드라이버를 거쳐 PWM 핀(MOTOR_PIN)에 연결하세요. (모터를 핀에 직접 연결하면 안 됩니다)
// 모듈 버전에서 망치 타격을 알릴 때는 아두이노가 "HIT", 버튼은 "BTN" 을 한 줄로 보내면 됩니다.

const int MOTOR_PIN = 9;        // PWM 핀 (UNO: 3,5,6,9,10,11)
unsigned long vibEndMs = 0;     // 진동이 끝날 시각
String line;

void setup() {
  pinMode(MOTOR_PIN, OUTPUT);
  analogWrite(MOTOR_PIN, 0);
  Serial.begin(9600);
}

void handleLine(String s) {
  s.trim();
  if (s.startsWith("VIB")) {
    int a = s.indexOf(' ');
    int b = s.indexOf(' ', a + 1);
    if (a < 0 || b < 0) return;
    int pwm = constrain(s.substring(a + 1, b).toInt(), 0, 255);
    long ms = max(1L, s.substring(b + 1).toInt());
    analogWrite(MOTOR_PIN, pwm);          // 세기 = PWM 듀티
    vibEndMs = millis() + ms;             // 시간이 지나면 loop()에서 끔 (새 명령이 오면 덮어씀)
    Serial.print("OK VIB "); Serial.print(pwm); Serial.print(' '); Serial.println(ms);  // Unity Console에서 확인용
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
}
