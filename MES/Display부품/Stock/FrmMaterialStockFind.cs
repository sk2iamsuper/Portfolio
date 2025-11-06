using System;
using System.Collections;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;

namespace SG_Module.Common
{
    public partial class FrmMaterialStockFind : DevExpress.XtraEditors.XtraForm
    {
        // 선택된 자재 정보 반환용 변수들 (팝업 → 호출부)
        public string strMaterialOrigID = string.Empty;
        public string strMaterialSpec = string.Empty;
        public string strMaterialWidth = string.Empty;
        public string strMaterialLen = string.Empty;
        public string strBatchID = string.Empty;
        public string strMaterialRWidth = string.Empty;

        // 외부에서 읽도록 Property 형태로 제공
        public string MaterialOrigID { get => strMaterialOrigID; set => strMaterialOrigID = value; }
        public string MaterialSpec { get => strMaterialSpec; set => strMaterialSpec = value; }
        public string MaterialWidth { get => strMaterialWidth; set => strMaterialWidth = value; }
        public string MaterialLen { get => strMaterialLen; set => strMaterialLen = value; }
        public string BatchID { get => strBatchID; set => strBatchID = value; }
        public string MaterialRWidth { get => strMaterialRWidth; set => strMaterialRWidth = value; }

        BaseEdit inplaceEditor;

        // ★ Grid Layout 저장 경로
        string layoutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyMESProgram", "Layout");
        string layoutFile => Path.Combine(layoutPath, $"{this.Name}_{gridView1.Name}.xml");

        // ★ 검색 조건 저장 경로
        string configPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyMESProgram", "Config");
        string configFile => Path.Combine(configPath, $"{this.Name}_Search.ini");

        public FrmMaterialStockFind()
        {
            InitializeComponent();
        }

        // 화면 Load 시 초기 설정, 이전 검색조건 불러오기, Grid Layout 복원
        private void FrmMaterialStockFind_Load(object sender, EventArgs e)
        {
            InitializeForm();
            LoadSearchCondition(); // ★ 마지막 사용 검색조건 복원
            LoadGridLayout();      // ★ 마지막 Grid UI 상태 복원
        }

        // 화면 종료 시 검색조건/그리드 상태 저장
        private void FrmMaterialStockFind_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSearchCondition();
            SaveGridLayout();
        }


        #region Grid Layout 저장 / 복원

        private void SaveGridLayout()
        {
            try
            {
                if (!Directory.Exists(layoutPath))
                    Directory.CreateDirectory(layoutPath);

                // 현 GridView의 컬럼 순서/너비/정렬/필터 상태 저장
                gridView1.SaveLayoutToXml(layoutFile);
            }
            catch { }
        }

        private void LoadGridLayout()
        {
            try
            {
                if (File.Exists(layoutFile))
                    gridView1.RestoreLayoutFromXml(layoutFile);
            }
            catch { }
        }

        #endregion


        #region 검색조건 저장 / 복원

        // 화면 종료 또는 검색 버튼 클릭 시 조건 저장
        private void SaveSearchCondition()
        {
            try
            {
                if (!Directory.Exists(configPath))
                    Directory.CreateDirectory(configPath);

                // 간단한 INI 형식으로 저장
                File.WriteAllText(configFile,
                    $"WAREHOUSE={lueWarehouse.EditValue}\r\n" +
                    $"MATTYPE={lueMaterialType.EditValue}\r\n" +
                    $"MATSPEC={txtMatSpec.Text}\r\n" +
                    $"MATW={txtMatW.Text}\r\n");
            }
            catch { }
        }

        // 화면 Load 시 저장된 조건 복원
        private void LoadSearchCondition()
        {
            try
            {
                if (!File.Exists(configFile)) return;

                foreach (var line in File.ReadAllLines(configFile))
                {
                    var kv = line.Split('=');
                    if (kv.Length != 2) continue;

                    switch (kv[0])
                    {
                        case "WAREHOUSE": lueWarehouse.EditValue = kv[1]; break;
                        case "MATTYPE": lueMaterialType.EditValue = kv[1]; break;
                        case "MATSPEC": txtMatSpec.Text = kv[1]; break;
                        case "MATW": txtMatW.Text = kv[1]; break;
                    }
                }
            }
            catch { }
        }

        #endregion


        // 화면 초기에 LookupEdit 및 Grid 초기화
        private void InitializeForm()
        {
            InitializeGridStock3(); // ★ 그리드 컬럼/포맷 등 초기화 함수

            try
            {
                // 창고 목록 바인딩
                string CommSql = " EXEC SP_PO_COMMON 'MATWAREHOUSE','','','','' ";
                DevLookupEditUtil.Instance.bindSLookupEdit_Sql(lueWarehouse, CommSql);

                // 자재 유형 목록 바인딩
                DevLookupEditUtil.Instance.bindLookupEdit_Sql(lueMaterialType, "SELECT CDE,NAME FROM vi_MaterialOrig ");
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message);
            }
        }

        // 검색 버튼 클릭
        private void btnFind_Click(object sender, EventArgs e)
        {
            fnSearch();
        }

        // Enter 키 입력 시 자동 검색
        private void txtMaterialSpec_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                fnSearch();
        }


        // 그리드에서 더블클릭 → 선택값 반환 → 팝업 종료
        private void gridView1_DoubleClick(object sender, EventArgs e)
        {
            GridView view = (GridView)sender;
            Point pt = view.GridControl.PointToClient(Control.MousePosition);

            if (view.FocusedRowHandle < 0) return;
            DoRowDoubleClick(view, pt);
        }

        // 선택 Row 에서 값 추출하여 팝업 호출부에 전달
        private void DoRowDoubleClick(GridView view, Point pt)
        {
            GridHitInfo info = view.CalcHitInfo(pt);
            if (!(info.InRow || info.InRowCell)) return;
            if (info.RowHandle < 0) return;

            MaterialOrigID = view.GetRowCellValue(info.RowHandle, "Material_ID").ToString();
            MaterialSpec = view.GetRowCellValue(info.RowHandle, "Material_Name").ToString();
            MaterialWidth = view.GetRowCellValue(info.RowHandle, "Material_Width").ToString();
            MaterialLen = view.GetRowCellValue(info.RowHandle, "Material_Length").ToString();
            BatchID = view.GetRowCellValue(info.RowHandle, "BatchID").ToString();
            MaterialRWidth = view.GetRowCellValue(info.RowHandle, "Material_RWidth").ToString();

            this.Close(); // 선택 후 팝업 종료
        }


        #region 검색 실행

        // 자재 재고 검색 수행 함수
        private void fnSearch()
        {
            try
            {
                string strSpName = " EXEC SP_MM_INVENTSTOCK_SELECT4_REV1  @WAREHOUSECDE,@MATTYPE,@MATSPEC,@MATW ";
                strSpName = strSpName.Replace("@WAREHOUSECDE", $"N'{lueWarehouse.EditValue}'");
                strSpName = strSpName.Replace("@MATTYPE", $"N'{lueMaterialType.EditValue}'");
                strSpName = strSpName.Replace("@MATSPEC", $"N'{txtMatSpec.Text.Trim()}'");
                strSpName = strSpName.Replace("@MATW", $"N'{txtMatW.Text}'");

                // DB 조회 실행
                DataTable dt = DatabaseUtil.Instance.ExecuteQuery(strSpName);

                // 결과 바인딩
                gridControl1.DataSource = dt;
                gridView1.BestFitColumns(); // 컬럼 너비 자동 정리
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message);
            }
        }

        #endregion

    }
}
