# MES+ 시스템 - 포트폴리오 프로젝트

## 📁 프로젝트 개요
**프로젝트명**: SSD MES+ 시스템  
**개발 기간**: 2024 ~ 2025  
**역할**: 풀스택 개발자  
**기술 스택**: C# WinForms, MySQL, .NET Framework 4.5+  

## 🎯 프로젝트 설명
이 프로젝트는 제조업체의 **제조 실행 시스템(MES)**으로, 생산 공정의 전 과정을 통합 관리하는 엔터프라이즈급 애플리케이션입니다. 한국, 베트남의 여러 공장(SPK-01, SPK-02, SPV 등)에서 운영되며, **다국어 지원(한국어/영어/베트남어)** 기능을 포함하고 있습니다.

## 🏗️ 시스템 아키텍처

### 📊 주요 모듈 구성
```
1. 생산지원 모듈 (Production Support)
   - 반출입 관리
   - 방문객 관리
   - 태블릿 모드

2. 자재관리 모듈 (Material Management)
   - 입고/출고 관리
   - 재고 조회
   - 자재 이동 추적
   - BOM 관리

3. 생산관리 모듈 (Production Management)
   - 생산 계획 및 실적 관리
   - 실시간 현황판
   - 작업 지시서 관리
   - 가동률 분석

4. 공정관리 모듈 (Process Management)
   - 실시간 공정 모니터링
   - 품질 점검 및 검사
   - 불량 관리 및 재작업
   - 장비 유지보수

5. 품질관리 모듈 (Quality Management)
   - 수입검사 관리
   - 공정검사(PQC/SPCN)
   - AQL 관리
   - 3S/8D 리포트

6. 기준정보 모듈 (Master Data)
   - E-SPEC 관리
   - BOM 구성
   - 설비/치공구 관리
   - 사용자 권한 관리
```

## 🛠️ 주요 기능 상세

### 🔐 로그인 및 권한 관리
- 다중 공장 지원 (SPK-01, SPK-02, SPV)
- 사용자별 권한 체계 구현
- 즐겨찾기 메뉴 커스터마이징
- 자동 로그인 정보 저장

### 🌐 다국어 지원 시스템
- 동적 메뉴 텍스트 로딩
- 실시간 언어 전환
- 데이터베이스 기반 메시지 관리

### 📧 통합 알림 시스템
- 이메일 알림 기능 (Naver SMTP)
- 공지사항 팝업
- 문제 보고 시스템(VOC)

### 🔍 고급 검색 기능
- 트리뷰 실시간 필터링
- 즐겨찾기 빠른 접근
- 탭 기반 멀티 문서 인터페이스

### 📊 실시간 모니터링
- 생산 현황 실시간 표시
- 장비 가동률 모니터링
- 재고 수준 추적

## 💻 기술적 특징

### 1. 데이터베이스 연동
```csharp
// MySQL 연결 관리
private MySqlConnection _connection;
_connection = Helpers.MySqlHelper.InitConnection(cbDBsite.Text);

// 동적 쿼리 실행
var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
```

### 2. 트리뷰 동적 생성
```csharp
// 공장별 메뉴 구조 자동 생성
private void treeview_vpk_1()
private void treeview_vpv()
private void treeview_vpk_2()
```

### 3. 폼 관리 시스템
```csharp
// 탭 기반 MDI 구현
TabPage tbp = new TabPage(SelectedNode.Text);
tabControl1.TabPages.Add(tbp);
tbp.Controls.Add(childForm);
```

### 4. 다국어 처리
```csharp
// 언어별 메시지 사전
public static Dictionary<string, string> dictionary = new Dictionary<string, string>();

// 동적 메뉴 텍스트 생성
public static string menuname(string msgcode, string krmsg)
{
    return dictionary[msgcode] + " .[" + msgcode + "]";
}
```

## 🚀 성과 및 기여

### 1. **생산성 향상**
- 실시간 데이터 접근성 향상

### 2. **품질 관리 강화**
- 추적 가능성 100% 확보
- 품질 리포트 자동화

### 3. **다국어 지원**
- 3개 언어(한국어/영어/베트남어) 지원
- 현지화된 사용자 인터페이스
- 문화적 차이 반영

### 4. **확장성 확보**
- 모듈형 아키텍처
- 새로운 공장 쉽게 추가 가능
- 사용자 정의 가능한 메뉴 구조

## 📈 기술적 도전과 해결

### 🔧 성능 최적화
- 더블 버퍼링 적용으로 UI 반응성 개선
- 트리뷰 상태 저장/복원 기능
- 데이터베이스 연결 풀링

### 🔒 보안 강화
- 사용자 권한 세분화
- 데이터 무결성 검증
- 안전한 로그인 프로세스

### 🌍 글로벌 대응
- 타임존 처리
- 통화 및 단위 변환
- 현지 법규 준수

## 📚 학습 포인트

1. **엔터프라이즈 애플리케이션 설계**
   - 복잡한 비즈니스 로직 모듈화
   - 확장 가능한 아키텍처 설계

2. **다국어 및 현지화**
   - 문화적 차이 고려한 UI 설계
   - 동적 리소스 관리

3. **데이터베이스 최적화**
   - 대용량 데이터 처리
   - 실시간 동기화

4. **사용자 경험 설계**
   - 직관적인 인터페이스
   - 효율적인 워크플로우

## 🔮 향후 발전 방향

1. **클라우드 마이그레이션**
2. **모바일 앱 확장**
3. **AI 기반 예측 분석**
4. **IoT 장비 통합**

