# WMS 시스템 포트폴리오

## 📋 프로젝트 개요
** WMS(Warehouse Management System)**는 화학 제조 업체의 창고 관리 및 생산 실행 시스템입니다. 이 프로젝트는 C# Windows Forms를 사용하여 개발된 현업에서 사용 중인 실시간 창고 관리 솔루션입니다.

## 🎯 프로젝트 목표
- **실시간 창고 관리**: 화학 제품의 입출고, 재고 관리, QC 검사 프로세스 자동화
- **생산 효율 향상**: 피킹, 포장, 검수 프로세스 최적화
- **데이터 정확도 보장**: 바코드 스캐닝과 실시간 데이터 동기화
- **멀티 공장 지원**: F11, F13 등 다양한 공장 환경 대응

## 🛠️ 기술 스택
- **플랫폼**: .NET Framework 4.7.2
- **언어**: C# (Windows Forms)
- **데이터베이스**: Microsoft SQL Server
- **버전 관리**: Git
- **개발 도구**: Visual Studio 2019+
- **기술적 특성**: 
  - 이벤트 기반 아키텍처
  - 동적 UI 생성
  - INI 파일 기반 설정 관리
  - 비동기 파일 업데이트 시스템

## 📁 프로젝트 구조
```
MES_WMS/
├── FrmMain.cs          # 메인 폼 - 시스템 중앙 컨트롤러
├── Picking.cs            # 피킹 관리 모듈
├── ManageQC.cs            # 검수 관리 모듈
├── InnerBox.cs            # 내부 박스 관리 모듈
├── Stock.cs            # 재고 설정 모듈
├── QC.cs            # QC 검사 모듈 ,품질검사(QC)를 위한 폼으로, LOT 관리, DP(도수) 선택, 검사 데이터 입력 및 ERP 연동 기능을 포함
├── PRF13.cs            # 사용자 설정 모듈
├── UserCommon/         # 공통 유틸리티
│   ├── Public_Function.cs
│   ├── CmCn.cs        # 데이터베이스 연결 관리
│   ├── ClsFileCtl.cs  # 파일 컨트롤러
│   └── ClsinitUtil.cs # INI 설정 관리
└── CLSORDERLIST.cs    # 주문 리스트 관리
```

## 🔧 주요 기능

### 1. 사용자 관리 시스템
- **다중 권한 관리**: 작업자, QC, 관리자별 권한 분리
- **실시간 인증**: 출퇴근 상태 확인 및 IP 기반 접근 제어
- **동적 UI**: 사용자 역할에 따른 인터페이스 자동 구성

### 2. 동적 UI 생성 엔진
```csharp
// 플렉서블한 버튼 생성 시스템
private void CreateDynamicButtons(
    DataSet dataSource,
    FlowLayoutPanel container,
    Color backColor,
    Color foreColor,
    ButtonType buttonType)
{
    // 데이터 기반 동적 UI 생성
    // 다양한 버튼 타입(WH, Factory, Empno) 지원
}
```

### 3. 자동 업데이트 시스템
- **버전 체크**: 서버와 로컬 실행 파일 버전 비교
- **무중단 업데이트**: AsyncMES를 통한 백그라운드 업데이트
- **설정 동기화**: 중앙 서버와 로컬 설정 파일 자동 동기화

### 4. 멀티 프린터 지원
```csharp
// 프린터 경로 자동 관리
public string GetSelectedPrinterPath()
{
    return rb_ip1.Checked ? rb_ip1.Text : rb_ip2.Text;
}
```

### 5. 실시간 데이터 처리
- **SQL Server 연동**: 실시간 재고 조회 및 업데이트
- **트랜잭션 관리**: 데이터 일관성 보장
- **에러 로깅**: 시스템 오류 추적 및 복구

## 🚀 성과 및 영향

### 1. 생산성 향상
- **실시간 재고 정확도 99.5% 달성**

### 2. 기술적 성과
- **동적 UI 생성 시스템**: 재사용 가능한 UI 컴포넌트 설계
- **이벤트 기반 아키텍처**: 느슨한 결합과 높은 확장성
- **자체 업데이트 메커니즘**: 배포 및 유지보수 효율화

### 3. 비즈니스 영향
- **3개 공장 동시 운영 지원**: 확장성 있는 아키텍처
- **24/7 운영 가능**: 안정적인 시스템 설계
- **사용자 친화적 인터페이스**: 최소한의 교육으로 운영 가능

## 💡 문제 해결 사례

### 1. 실시간 데이터 동기화 문제
**문제**: 여러 공장에서 동시 접속 시 데이터 불일치 발생  
**해결**: 
- 이벤트 드리븐 아키텍처 도입
- 데이터베이스 트랜잭션 격리 수준 최적화
- 낙관적 동시성 제어 구현

### 2. 대용량 데이터 처리 성능
**문제**: 10만 건 이상의 재고 데이터 처리 지연  
**해결**:
- 지연 로딩(Lazy Loading) 패턴 적용
- 데이터베이스 인덱스 최적화
- 메모리 캐싱 시스템 구현

### 3. 크로스 플랫폼 호환성
**문제**: 32비트/64비트 시스템 호환성 문제  
**해결**:
- 환경 감지 로직 구현
- 조건부 파일 경로 처리
- 자동 시스템 아키텍처 감지



---

**이 프로젝트는 실제 현업 환경에서 운영되며, 지속적인 개선과 최적화를 통해 생산성과 정확성을 높이는 솔루션입니다.**

FrmMain
<img width="1173" height="713" alt="WMS 실행화면" src="https://github.com/user-attachments/assets/ddad573a-c8b7-4300-ba41-9dd39418a20b" />

<img width="1267" height="782" alt="스크린샷 2026-01-21 21 14 53" src="https://github.com/user-attachments/assets/174d8134-2d72-4781-a696-80663c899655" />


Picking
<img width="689" height="823" alt="image" src="https://github.com/user-attachments/assets/124d1f8d-625b-4ca1-ab39-1c35b0e8c4ed" />

InnerBox
<img width="1184" height="812" alt="image" src="https://github.com/user-attachments/assets/e225f1e4-9afd-4503-b75f-ac0fac4e53c4" />

Stock
<img width="919" height="795" alt="image" src="https://github.com/user-attachments/assets/61554542-d023-4c8f-9637-f70048e4da95" />

QC
<img width="1324" height="878" alt="image" src="https://github.com/user-attachments/assets/586baaa4-ea89-40a2-b7d2-eaa57f289465" />
