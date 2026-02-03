# mes+

WinForms 기반 제조 MES 클라이언트 애플리케이션입니다. 생산/품질/자재/공정 등 제조 운영 전반을 화면 단위로 제공하고, MySQL 등 데이터베이스와 연동되어 현장 데이터를 처리합니다.

## Overview
- .NET Framework 4.8 WinForms 애플리케이션
- 다수의 제조 공정/품질/자재/출하 화면 모듈 포함
- DB 연동 기반의 현장 운영 시스템

## Tech Stack
- Language: C#
- Framework: .NET Framework 4.8, WinForms
- Database: MySQL (필수), Oracle/SQL Server (프로젝트 내 참조 존재)
- Logging: NLog

## Requirements
- Windows 환경
- Visual Studio (권장: 2019 이상)
- .NET Framework 4.8 Developer Pack
- MySQL 클라이언트/접속 정보

## Build & Run
1. `mes+.sln`을 Visual Studio로 오픈
2. NuGet 패키지 복원
3. `mes+` 프로젝트를 시작 프로젝트로 설정
4. 빌드 후 실행

## Configuration
아래 파일에 환경별 설정이 포함되어 있습니다.
- `mes+/App.config`: DB 연결 문자열 및 앱 설정
- `mes+/NLog.config`: 로그 출력 설정

## Project Structure
- `mes+/01. Factory_02/` 제조/공정 관련 화면
- `mes+/04. Search/` 조회/현황 화면
- `mes+/05. Plan/` 계획/스케줄 화면
- `mes+/06. smt/` SMT 공정 화면
- `mes+/07. MaterialWarehouse/` 자재/창고 화면
- `mes+/08. bom/` BOM 관련 화면
- `mes+/09. 기준정보/` 기준정보 관리
- `mes+/10. QualityManagement/` 품질 관리
- `mes+/11. Management/` 관리 기능
- `mes+/20. step_in_out/` 공정 IN/OUT 및 공정별 기능
- `mes+/25. ShipMent/` 출하 관리
- `mes+/40. 예외처리/` 예외 처리
- `mes+/50. 불량관리/` 불량 관리
- `mes+/60. 생산지원/` 생산 지원
- `mes+/70. ERP/` ERP 연계

## UI Highlights
프로젝트 리소스에 포함된 주요 화면 이미지를 발췌했습니다.

![MES Main UI](mes+/Resources/MES-3.png)
![MES Screen](mes+/Resources/MES-1.png)
![MES Theme](mes+/Resources/mes_spring.png)

## Notes
- 프로젝트 내 일부 참조 DLL은 로컬 경로에 의존할 수 있습니다. (예: Excel Interop)
- 환경별 DB 스키마/테이블이 필요합니다.
