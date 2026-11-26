# GoogleSignIn 1.0.4 네이티브 재빌드

공식 `googlesamples/google-signin-unity`의 `v1.0.4` 커밋
`588c0637310623d86c55d06f0adc72972b0bac22`만 사용한다.
`source-manifest.json`의 원본 AAR·소스·패치 SHA256이 다르면 빌드 전에 중단한다.
원본 라이선스는 `LICENSE`에 보존한다.

Python 3.9 이상, NDK r27c `27.2.12479018`, CMake 3.22.1, Ninja가 필요하다.
공식 저장소의 위 커밋을 내려받아 압축 해제한 루트와 **변경 전** 1.0.4 srcaar를 지정한다.
현재 프로젝트의 수정된 srcaar는 원본 입력으로 사용하지 않는다.

```text
python build_native.py --source <공식_소스_루트> --original-aar <원본_1.0.4.srcaar> --ndk <NDK_r27c_루트> --output <외부_QA_또는_무시된_build_폴더>
```

CMake를 자동으로 찾지 못하면 `--cmake <cmake_실행파일>`을 추가한다.
Unity·기기·서버를 실행하지 않고, 입력 소스와 AAR도 수정하지 않는다.
출력은 입력·도구와 겹치지 않는 새 폴더 또는 빈 폴더를 사용한다.
산출물·컴파일 로그·`verification.json`은 output에만 생성하며 커밋하지 않는다.

ARM64와 ARMv7을 Android API 21, C++11, `c++_static`, Release로 빌드한다.
Windows 짧은 실행 파일명에서도 C++ 드라이버를 선택하도록 `--driver-mode=g++`를 명시한다.
ARM64에만 `-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384`를 적용한다.
`source-redaction.patch`는 무조건 실행되던 사용자 문자열·표시 이름 INFO 로그와 주소 덤프
4곳을 제거한다. 값 없는 configuration 오류 로그와 인증 데이터 흐름은 유지한다.

공개 C/JNI 20개, SONAME, DT_NEEDED, RELRO, W^X, 비실행 스택, 민감 출력 상수 부재를 검사한다.
ARM64는 PT_LOAD 16KB 정렬과 RELRO 뒤 같은 페이지의 writable-tail 부재도 검사한다.
다른 AAR 내용(Java·manifest·x86 포함)은 바이트 단위로 보존하고 체크섬 파일을 갱신한다.
NDK의 내부 STL·링커 dynsym 차이는 전체 목록을 기록하며 전체 심볼 동일을 주장하지 않는다.

기준 산출물 SHA256은 `source-manifest.json`에 기록한다. 빌드 경로와 도구 정보에 따라
Build-ID·파일 SHA가 달라질 수 있으므로 각 제작 결과의 `verification.json`도 보존한다.
실제 Google 로그인·16KB 기기·Google Play 승인은 별도로 검증해야 한다.
