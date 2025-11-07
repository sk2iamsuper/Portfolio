#region namespace
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraEditors.Repository;
using System.Data.SqlClient;
using DevCommon;
#endregion

namespace MES_WMS
{
    public partial class FrmMaterialStock : XtraForm
    {
        // 🔹 DB 연결 문자열 (실환경에 맞게 수정)
        private readonly string connectionString = "Server=YOUR_SERVER;Database=YOUR_DB;User Id=USER;Password=PWD;";

        // 🔹 나중에 PLC 연동용으로 확장 가능한 클래스 구조
        private readonly PlcManager plcManager = new PlcManager();

        public FrmMaterialStock()
        {
            InitializeComponent();
            InitGrid(); // 그리드 초기화
        }

        #region Form Events
        private async void FrmMaterialStock_Load(object sender, EventArgs e)
        {
            // 폼 로드시 데이터 로드
            await LoadStockDataAsync();
        }
        #endregion

        #region Grid 설정
        /// <summary>
        /// DevExpress GridControl과 GridView를 초기화
        /// </summary>
        private void InitGrid()
        {
            // 그리드뷰 속성 설정
            gridView1.OptionsBehavior.Editable = false;
            gridView1.OptionsView.ShowGroupPanel = false;
            gridView1.OptionsView.ColumnAutoWidth = false;
            gridView1.OptionsBehavior.AllowAddRows = DevExpress.Utils.DefaultBoolean.False;

            // 컬럼 설정
            gridView1.Columns.Clear();

            gridView1.Columns.AddVisible("ItemCode", "품목코드").Width = 120;
            gridView1.Columns.AddVisible("ItemName", "품명").Width = 200;
            gridView1.Columns.AddVisible("Qty", "수량").Width = 100;
            gridView1.Columns.AddVisible("Location", "위치").Width = 150;

            // 숫자 정렬
            gridView1.Columns["Qty"].AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
        }
        #endregion

        #region 데이터 로드
        /// <summary>
        /// 비동기 방식으로 자재 재고 데이터를 DB에서 조회
        /// </summary>
        private async Task LoadStockDataAsync()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT 
                            ItemCode AS 품목코드,
                            ItemName AS 품명,
                            Qty AS 수량,
                            Location AS 위치
                        FROM MaterialStock
                        ORDER BY ItemCode";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        // UI 스레드에서 Grid에 바인딩
                        gridControl1.Invoke(new Action(() =>
                        {
                            gridControl1.DataSource = dt;
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"데이터 로드 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region PLC 연동 구조 (향후 구현용)
        /// <summary>
        /// PLC 통신 관련 로직을 캡슐화할 클래스
        /// </summary>
        public class PlcManager
        {
            public bool IsConnected { get; private set; } = false;

            public void Connect(string ip, int port)
            {
                // TODO: PLC 통신 초기화 (예: ModbusTCP, Ethernet/IP 등)
                // 현재는 구조만 남겨둔 상태
                IsConnected = true;
            }

            public void Disconnect()
            {
                // TODO: PLC 연결 해제
                IsConnected = false;
            }

            public string ReadData(string address)
            {
                // TODO: PLC로부터 데이터 읽기
                return "0";
            }

            public void WriteData(string address, string value)
            {
                // TODO: PLC에 데이터 쓰기
            }
        }
        #endregion
    }
}
