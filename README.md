# VR Arcade Demo (Meta Quest + 하우징 센서)

한국 인형뽑기방 느낌의 VR 오락실에서 즐기는 두 가지 게임입니다.

| 씬 | 게임 |
|---|---|
| `Assets/Scenes/WhackAMole.unity` | 두더지 잡기 (구멍 한 줄 5개, 1단계 드럼 스틱 → 2단계 망치) |
| `Assets/Scenes/ShootingRange.unity` | 사격 (5 m 앞 3×3 원형 표적, 30초, 레이저 조준) |

게임 안 전환: 왼손 **Y** → 확인 창에서 **Y**(이동) / **X**(취소)

---

## 1. 다른 PC에서 열기

1. **Git LFS**를 먼저 설치하세요 (그림·소리·폰트·3D 모델이 LFS로 저장되어 있음).
   ```
   git lfs install
   git clone https://github.com/lgh011224-sketch/Demo.git
   ```
   ZIP으로 받으면 LFS 파일이 빠질 수 있으니 `git clone`을 권장합니다.
2. **Unity Hub**에서 Unity **6000.6.3f1** (Unity 6.6)을 설치하고 프로젝트 폴더를 엽니다.
   첫 실행 때 `Library` 폴더를 새로 만드느라 시간이 걸립니다.
3. **WhackAMole** 씬을 열고 Play.

### Quest 연결 (Quest Link)
- Meta Quest Link(PC 앱)를 설치하고 헤드셋을 Link로 연결합니다.
- Meta Quest Link 앱 → 설정 → 일반 → **알 수 없는 출처(Unknown Sources)** 켜기
- Windows 설정에서 OpenXR 런타임이 Meta Quest Link인지 확인
- Unity 에디터는 **DX11**로 실행 (DX12면 헤드셋 화면이 안 나올 수 있음)

---

## 2. 아두이노 + 하우징 센서

### 업로드할 코드
**`Arduino/ImuHaptic/ImuHaptic.ino`** 를 Arduino IDE로 열어 보드(Arduino Uno)에 업로드합니다.
(`Arduino/HapticReceiver/` 는 센서 없이 진동만 쓰던 예전 버전)

### 배선 (Arduino Uno)
| 센서 (GY-6500 / GY-9250) | 아두이노 |
|---|---|
| VCC | 5V |
| GND | GND |
| SDA | A4 |
| SCL | A5 |
| NCS | 3.3V |
| AD0 | GND |
| (진동 모터 – 드라이버 경유) | 9번 핀 |
| (선택) 발사 버튼 | 2번 핀 ↔ GND |
| (선택) 가운데 맞추기 버튼 | 3번 핀 ↔ GND |

시리얼 모니터(115200bps)에 `IMU OK 0x71` 이 나오면 정상입니다.
**업로드 후에는 시리얼 모니터를 닫아야** Unity가 포트를 쓸 수 있습니다.

### Unity에서 포트 설정
PC마다 COM 번호가 다릅니다 (장치 관리자 → 포트(COM & LPT) 에서 확인).
두 씬 모두 Hierarchy의 **Arduino** 오브젝트 → `ArduinoSerial` → **Port Name** 을 바꾸세요 (현재 `COM7`).
아두이노가 연결되어 있지 않으면 마우스로 시험할 수 있습니다 (마우스 이동 = 방향, 클릭 = 내려치기).

### 처음 한 번: 센서 방향 맞추기 (PC마다 한 번)
Play 후 VR 화면 안내를 따릅니다. 각 단계는 컨트롤러 **그립(중지)** 버튼으로 저장합니다.
1. 하우징을 내려놓고 가만히 (자이로 영점)
2. 칼끝을 **하늘로** 세우고 → 그립
3. 칼을 **정면으로 수평**으로 들고 → 그립
4. 손잡이 축으로 **좌우로 비틀고** → 그립

다시 하려면 오른손 썸스틱 누르기 (키보드 K). 정면 다시 맞추기는 오른손 그립 (키보드 C).

---

## 3. 조작

| | 두더지 잡기 | 사격 |
|---|---|---|
| 시작 | 오른손 A (Space) | 오른손 A (Space) |
| 치기 / 쏘기 | 하우징을 빠르게 내려쳤다 멈추기 | 오른손 트리거 (F) / 2번 핀 버튼 |
| 조준·구멍 고르기 | 하우징을 좌우로 돌리기 | 하우징 방향 (레이저 점이 맞는 곳) |

자세한 설정·조절값은 `Assets/WhackAMole/설치_안내.md` 참고.

## 4. 다시 만들기 (에디터 메뉴)
- `Tools > Arcade > Rebuild Background` – 오락실 배경
- `Tools > Arcade > Rebuild Whack-A-Mole Cabinet` – 두더지 오락기
- `Tools > Arcade > Build Shooting Gallery` – 사격 부스

## 사용한 외부 자료 (모두 무료 라이선스)
- Poly Haven 텍스처·HDRI (CC0)
- BigSoundBank 권총 사격음 #0438 (CC0) – `Assets/ShootingRange/Audio/BigSoundBank/LICENSE.txt`
- 글꼴: Noto Sans KR (OFL), Luckiest Guy (Apache 2.0)
