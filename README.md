---

# Unity 파트 인수인계 문서

프로젝트명: PGWUnity
작성자: Unity 담당

---

## 1. 프로젝트 개요

본 Unity 프로젝트는 사용자가 선택한 의류를 아래 두 가지 방식으로 착용해볼 수 있도록 구성되어 있다.

1. 정적 피팅 (마네킹 기반)

* 기준 마네킹에 의류를 착용하는 방식
* 체형 변형 없이 프리셋 기반 착용

2. 실시간 AR 피팅 (카메라 + MediaPipe 기반)

* 카메라 입력 기반 실시간 관절 추적
* 사용자 움직임에 따라 의류가 반응

Android 앱에서 사용자의 선택에 따라 Unity 씬을 분기하며,
이후 서버 또는 Android 앱에서 전달받은 JSON 데이터를 기반으로 의류 및 색상을 적용한다.

---

## 2. Unity 개발 환경

Unity Editor 버전: 2022.3.62f3 (LTS)
빌드 방식: Android Export Project (unityLibrary 구조)

※ Unity 버전 변경 시 MediaPipe 및 Android 연동 충돌 가능성 있음.
※ LTS 버전 유지 권장.

---

## 3. 씬 구성

프로젝트에는 다음 두 개의 주요 씬이 존재한다.

### 3.1 FittingScene

* 마네킹 기반 정적 피팅 씬
* 사용자가 “마네킹에 입어보기” 선택 시 진입

### 3.2 MainARScene

* 카메라 + MediaPipe 기반 실시간 AR 피팅 씬
* 사용자가 “직접 입어보기” 선택 시 진입

---

## 4. 사용자 선택에 따른 씬 분기 구조

Android UI에서 다음 두 버튼을 제공한다.

* “마네킹에 입어보시겠습니까?” → FittingScene 로드
* “직접 입어보시겠습니까?” → MainARScene 로드

구현 방식은 다음 중 하나로 구성 가능하다.

1. Android에서 Unity 씬 직접 로드
2. Unity 내부 SceneRouter를 통한 씬 전환

※ 씬이 완전히 로드된 이후 JSON 전달을 권장한다.

---

## 5. 의류 적용 시스템 구조

두 씬 모두 동일한 의류 적용 시스템을 사용한다.

### 5.1 GameObject 정보

GameObject 이름: OutfitController
Component: OutfitController.cs
주요 메서드: ApplyOutfitJson(string json)

---

### 5.2 Android 호출 방식

Android는 반드시 아래 방식으로 호출해야 한다.

UnitySendMessage(
"OutfitController",
"ApplyOutfitJson",
jsonString
);

---

### 5.3 주의사항

* GameObject 이름은 반드시 "OutfitController"와 정확히 일치해야 한다.
* 메서드명 "ApplyOutfitJson"도 정확히 일치해야 한다.
* 해당 오브젝트는 씬에 활성 상태로 존재해야 한다.
* 씬 로드 완료 이후 호출해야 정상 동작한다.

---

## 6. JSON 데이터 구조 (고정 인터페이스)

Unity는 아래 구조의 JSON을 기대한다.

{
"topKey": "string",
"bottomKey": "string",
"topColor": "#RRGGBB",
"bottomColor": "#RRGGBB"
}

---

### 6.1 필드 설명

topKey

* 상의 프리팹 키
* null 또는 빈 문자열일 경우 상의 전체 비활성화

bottomKey

* 하의 프리팹 키
* null 또는 빈 문자열일 경우 하의 전체 비활성화

topColor

* 상의 색상 HEX 코드 (예: "#FF0000")

bottomColor

* 하의 색상 HEX 코드 (예: "#0000FF")

---

### 6.2 JSON 예시

{"topKey":"TOP_HOODIE_001","bottomKey":"BOTTOM_PANTS_003","topColor":"#FF0000","bottomColor":"#0000FF"}

---

## 7. 프리팹 키 규칙 (prefabKey)

Unity는 key → GameObject 매핑 구조를 사용한다.

해당 키는 DB / 서버 / Android / Unity 전 시스템 공통 식별자로 사용된다.

키는 변경하지 않는 것을 원칙으로 한다.
키 변경 시 전 시스템 수정이 필요하다.

---

### 7.1 권장 네이밍 규칙

상의 예시
TOP_SHIRT_001
TOP_HOODIE_001
TOP_JACKET_002
TOP_TSHIRT_003

하의 예시
BOTTOM_PANTS_001
BOTTOM_SKIRT_002
BOTTOM_SHORTS_001

---

## 8. Unity 내부 동작 로직 요약

ApplyOutfitJson 호출 시 다음 순서로 동작한다.

1. JSON 파싱
2. topKey에 해당하는 상의만 활성화
3. bottomKey에 해당하는 하의만 활성화
4. 전달된 HEX 색상 값 적용

색상 적용 방식

* MaterialPropertyBlock 사용
* _BaseColor 또는 _Color 프로퍼티에 적용

키가 존재하지 않을 경우

* 경고 로그 출력
* 기본 설정에 따라 이전 상태 유지 가능

---

## 9. Android 담당자 구현 가이드

### 9.1 씬 선택

Android 버튼 선택 후 Unity에 씬 로드 요청

FittingScene
MainARScene

씬 로드 완료 이후 JSON 전달 권장

---

### 9.2 의류 적용 호출

UnityPlayer.UnitySendMessage(
"OutfitController",
"ApplyOutfitJson",
jsonString
);

※ 씬이 완전히 로드된 이후 호출해야 안전함

---

## 10. 서버/DB 연동 주의사항

서버 또는 DB는 반드시 다음 정보를 포함해야 한다.

* topKey
* bottomKey
* topColor
* bottomColor

Unity는 key 기반으로만 동작하므로
서버 응답에 prefabKey가 반드시 포함되어야 한다.

---

## 11. 테스트 절차

1. Unity Editor에서 각 씬 실행
2. Inspector 또는 테스트 코드로 JSON 직접 입력
3. 의류 활성화 여부 확인
4. 색상 적용 확인
5. Android 연동 후 UnitySendMessage 호출 테스트

---

## 12. 향후 확장 가능 항목

* SceneRouter 추가 구현
* 추천 알고리즘 기반 자동 코디 시스템
* 프리팹 키 목록 CSV 공유 시스템
* DB 기반 동적 카탈로그 로딩
* 프리팹 자동 등록 매니저 시스템

---

필요 시 추가 제공 가능 항목

* 프리팹 키 목록 템플릿 파일
* Android-Unity 연동 예제 코드

---

이 문서는 Unity 파트 전체 구조 및 연동 인터페이스 기준 문서로 사용한다.
