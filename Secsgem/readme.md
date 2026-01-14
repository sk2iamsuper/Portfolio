본 프로젝트는 C# 기반 SECS/GEM(HSMS) 통신 구조를 직접 설계·구현하여
반도체/제조 장비와 MES(Host) 간의 Host–Equipment 통신 흐름을 코드 수준에서 재현한 포트폴리오입니다.
단순 라이브러리 사용이 아닌,  SECS-II 메시지 구조,  HSMS 세션 상태,  Host / Equipment 역할 분리를 중심으로 실제 제조 현장에서 발생하는 통신 시나리오를 기준으로 구성되었습니다.

System Architecture
[MES / Host  (.NET)]
      │
      │  HSMS (TCP/IP, Async Socket)
      ▼
[SecsGem Core Library (C#)]
      │
      │  SECS-II Message / State Machine
      ▼
[Equipment Simulator (.NET)]

