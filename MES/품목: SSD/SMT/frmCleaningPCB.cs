using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlHelper = MySql.Data.MySqlClient.MySqlHelper;
using System.Windows.Forms.DataVisualization.Charting;

namespace mes_
{
    public partial class frmPCB세척 : Form
    {
        #region 상수 정의
        private const string ADMIN_USER1 = "root";
        private const string ADMIN_USER2 = "admin";
        private const int PCB_SERIAL_LENGTH = 32;
        private const int TIME_DIFF_LIMIT_MINUTES = 120; // 2시간 제한 (현재 주석처리됨)
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
        #endregion

        #region 필드
        private readonly MySqlConnection _connection;
        private readonly List<string> _lineNames = new List<string> { "1 LINE", "2 LINE", "3 LINE", "4 LINE", "5 LINE" };
        private readonly List<string> _summaryTypes = new List<string> { "IN", "OUT", "LOSS", "YIELD", "PPM" };
        #endregion

        public frmPCB세척(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            InitializeDataGridViews();
        }

        /// <summary>
        /// 데이터그리드뷰 초기 설정
        /// </summary>
        private void InitializeDataGridViews()
        {
            // 차트 데이터그리드뷰 컬럼 설정
            InitializeChartDataGridViews();
            
            // 요약 데이터그리드뷰 초기화
            InitializeSummaryDataGridViews();
        }

        /// <summary>
        /// 차트용 데이터그리드뷰 초기화
        /// </summary>
        private void InitializeChartDataGridViews()
        {
            var chartDgvs = new[] { dgvChartMain, dgvChartLine1, dgvChartLine2, dgvChartLine3, dgvChartLine4, dgvChartLine5 };
            
            foreach (var dgv in chartDgvs)
            {
                dgv.Rows.Clear();
                dgv.Rows.Add("Loss수량", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0");
                dgv.Rows.Add("점유율 %", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0");
                dgv.Rows.Add("PPM", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0");
            }
        }

        /// <summary>
        /// 요약 데이터그리드뷰 초기화
        /// </summary>
        private void InitializeSummaryDataGridViews()
        {
            // Daily Summary 초기화
            InitializeDailySummary();
            
            // Line Summary 초기화
            InitializeLineSummary();
            
            // All Summary 초기화
            InitializeAllSummary();
        }

        /// <summary>
        /// 일별 요약 데이터그리드뷰 초기화
        /// </summary>
        private void InitializeDailySummary()
        {
            // 기존 컬럼 제거 (첫 두 컬럼 제외)
            for (int i = dgvDailySum.ColumnCount - 1; i >= 2; i--)
            {
                dgvDailySum.Columns.RemoveAt(i);
            }

            dgvDailySum.Rows.Clear();
            
            // 각 라인별로 5개의 행 추가 (IN, OUT, LOSS, YIELD, PPM)
            foreach (var lineName in _lineNames)
            {
                dgvDailySum.Rows.Add(lineName, "IN");
                dgvDailySum.Rows.Add(" ", "OUT");
                dgvDailySum.Rows.Add(" ", "LOSS");
                dgvDailySum.Rows.Add(" ", "YIELD");
                dgvDailySum.Rows.Add(" ", "PPM");
            }

            // 날짜별 컬럼 추가
            DateTime startTime = dtpStart.Value;
            DateTime endTime = dtpEnd.Value;
            
            for (var date = startTime; date <= endTime; date = date.AddDays(1))
            {
                AddDateColumnToDailySummary(date.ToString("MM-dd"));
            }

            // 스타일 적용
            ApplyDailySummaryStyles();
        }

        /// <summary>
        /// 일별 요약에 날짜 컬럼 추가
        /// </summary>
        private void AddDateColumnToDailySummary(string dateString)
        {
            DataGridViewColumn column = new DataGridViewTextBoxColumn
            {
                Name = dateString,
                HeaderText = dateString,
                Width = 60,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            
            dgvDailySum.Columns.Add(column);

            // 모든 행의 해당 컬럼 값을 0으로 초기화
            foreach (DataGridViewRow row in dgvDailySum.Rows)
            {
                row.Cells[column.Index].Value = "0";
            }
        }

        /// <summary>
        /// 일별 요약 스타일 적용
        /// </summary>
        private void ApplyDailySummaryStyles()
        {
            for (int i = 0; i < dgvDailySum.RowCount; i += 5)
            {
                dgvDailySum.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
            }
        }

        /// <summary>
        /// 라인별 요약 초기화
        /// </summary>
        private void InitializeLineSummary()
        {
            dgvLineSum.Rows.Clear();
            
            foreach (var lineName in _lineNames)
            {
                dgvLineSum.Rows.Add(lineName, "0", "0", "0", "0", "0");
            }
            
            ApplySummaryRowStyles(dgvLineSum, Color.LightCyan);
        }

        /// <summary>
        /// 전체 요약 초기화
        /// </summary>
        private void InitializeAllSummary()
        {
            dgvAllSum.Rows.Clear();
            dgvAllSum.Rows.Add("ALL", "0", "0", "0", "0", "0");
            ApplySummaryRowStyles(dgvAllSum, Color.LightCyan);
        }

        /// <summary>
        /// 요약 행 스타일 적용
        /// </summary>
        private void ApplySummaryRowStyles(DataGridView dgv, Color backColor)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                row.Cells[0].Style.BackColor = backColor;
            }
        }

        private void frmPCBCleaning_Load(object sender, EventArgs e)
        {
            // 관리자 권한 확인
            btnUpdate.Visible = IsAdminUser();
            
            // 초기 데이터 로드
            GetList("INSERT");
        }

        /// <summary>
        /// 관리자 사용자 여부 확인
        /// </summary>
        private bool IsAdminUser()
        {
            return frmMain.user_ID == ADMIN_USER1 || frmMain.user_ID == ADMIN_USER2;
        }

        private void txtScandata_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrEmpty(txtScandata.Text))
            {
                ProcessScannedData();
            }
        }

        /// <summary>
        /// 스캔된 데이터 처리
        /// </summary>
        private void ProcessScannedData()
        {
            try
            {
                // 데이터 정제
                string scannedData = txtScandata.Text.ToUpper().Split(' ')[0];
                txtScandata.Text = string.Empty;

                if (scannedData.Length == PCB_SERIAL_LENGTH)
                {
                    txtPcbSerial.Text = scannedData;
                    DeterminePCBStep();
                    ValidatePCBData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 처리 중 오류 발생: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// PCB 단계 결정 (1st/2nd)
        /// </summary>
        private void DeterminePCBStep()
        {
            // 첫 번째 문자로 1st/2nd 결정
            cbStep.Text = (txtPcbSerial.Text.Substring(0, 1) == "1") ? "1st" : "2nd";

            // 추가 검증을 통한 단계 결정
            string sql = $@"SELECT tob FROM tb_mes_std_espec 
                          WHERE id = (SELECT prod_id FROM tb_mes_sch_daily 
                          WHERE id = (SELECT DISTINCT dailyorder_id FROM tb_mes_dat_setinfo 
                          WHERE pcbserial LIKE '1{txtPcbSerial.Text.Substring(1, 31)}_'))";
            
            var dataTable = ExecuteQuery(sql);
            foreach (DataRow row in dataTable.Rows)
            {
                if (row[0].ToString() == "TOP")
                {
                    cbStep.Text = "1st";
                    break;
                }
            }
        }

        /// <summary>
        /// PCB 데이터 유효성 검증
        /// </summary>
        private void ValidatePCBData()
        {
            string sql = (cbStep.Text == "1st") 
                ? $"SELECT TIMESTAMPDIFF(MINUTE, created_on, NOW()) FROM tb_mes_dat_setinfo WHERE pcbserial like '1{txtPcbSerial.Text.Substring(1, 31)}1' "
                : $"SELECT TIMESTAMPDIFF(MINUTE, updated_at, NOW()) FROM tb_mes_dat_setinfo WHERE pcbserial like '1{txtPcbSerial.Text.Substring(1, 31)}1' ";

            string diffmin = Helpers.MySqlHelper.GetOneData(_connection, sql);
            
            if (diffmin == "Empty")
            {
                MessageBox.Show("정보를 찾을수 없습니다.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPcbSerial.Text = "";
            }
            /*
            // 2시간 제한 검증 (현재 주석 처리됨)
            else if (int.Parse(diffmin) > TIME_DIFF_LIMIT_MINUTES)
            {
                MessageBox.Show("투입금지 (TIMER OVER 2HOUR)", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPcbSerial.Text = "";
            }
            */
        }

        private void GetList(string role)
        {
            dgvMainList.Rows.Clear();
            
            if (role == "INSERT")
            {
                LoadRealTimeData();
            }
            else // SEARCH
            {
                InitializeSearchData();
                LoadSearchData();
            }
        }

        /// <summary>
        /// 실시간 데이터 로드
        /// </summary>
        private void LoadRealTimeData()
        {
            string sql = $@"SELECT h.id, date_format(h.created_on, '%Y-%m-%d %H:%i:%s'), pcb_serialnumber, e.series, 
                           t.line_name, h.comment, cleaning, result, u.user_id 
                           FROM tb_smt_cleaning_history h, tb_user u, tb_mes_dat_setinfo s, tb_mes_sch_daily d, 
                           tb_mes_std_espec e, tb_mes_sch_smt m, tb_mrp_std_line t 
                           WHERE h.user_id = u.id AND CONCAT('1', SUBSTR(h.pcb_serialnumber, 2, 31), '1') = s.pcbserial 
                           AND s.dailyorder_id = d.id AND d.prod_id = e.id AND d.id = m.dailyorder_id 
                           AND SUBSTR(h.pcb_serialnumber, 1, 1) = m.step_id AND m.line_id = t.id 
                           AND h.created_on > date_format(NOW(),'%Y-%m-%d 00:00:00') 
                           ORDER BY h.created_on";

            var dataTable = ExecuteQuery(sql);
            string prevsn = "";
            
            foreach (DataRow row in dataTable.Rows)
            {
                AddRowToMainList(row);
                
                // 중복 데이터 표시
                if (prevsn == row[2].ToString())
                {
                    dgvMainList.Rows[dgvMainList.RowCount - 1].Cells[2].Style.ForeColor = Color.Red;
                }
                prevsn = row[2].ToString();
            }
        }

        /// <summary>
        /// 검색 데이터 초기화
        /// </summary>
        private void InitializeSearchData()
        {
            InitializeChartDataGridViews();
            InitializeSummaryDataGridViews();
        }

        /// <summary>
        /// 검색 데이터 로드
        /// </summary>
        private void LoadSearchData()
        {
            LoadMainListData();
            LoadLossQuantityData();
            LoadProductionQuantityData();
            CalculateSummaryData();
            UpdateCharts();
        }

        /// <summary>
        /// 메인 리스트 데이터 로드
        /// </summary>
        private void LoadMainListData()
        {
            string sql = $@"SELECT h.id, date_format(h.created_on, '%Y-%m-%d %H:%i:%s'), pcb_serialnumber, e.series, 
                           t.line_name, h.comment, cleaning, result, u.user_id 
                           FROM tb_smt_cleaning_history h, tb_user u, tb_mes_dat_setinfo s, tb_mes_sch_daily d, 
                           tb_mes_std_espec e, tb_mes_sch_smt m, tb_mrp_std_line t 
                           WHERE h.user_id = u.id AND CONCAT('1', SUBSTR(h.pcb_serialnumber, 2, 31), '1') = s.pcbserial 
                           AND s.dailyorder_id = d.id AND d.prod_id = e.id AND d.id = m.dailyorder_id 
                           AND SUBSTR(h.pcb_serialnumber, 1, 1) = m.step_id AND m.line_id = t.id 
                           AND h.created_on > '{dtpStart.Value:yyyy-MM-dd 00:00:00}' 
                           AND h.created_on < '{dtpEnd.Value:yyyy-MM-dd 23:59:59}' 
                           ORDER BY h.created_on";

            var dataTable = ExecuteQuery(sql);
            string prevsn = "";
            int totalLoss = 0;
            int[] lineLosses = new int[5]; // 5개 라인

            foreach (DataRow row in dataTable.Rows)
            {
                AddRowToMainList(row);
                
                // 중복 데이터 표시
                if (prevsn == row[2].ToString())
                {
                    dgvMainList.Rows[dgvMainList.RowCount - 1].Cells[2].Style.ForeColor = Color.Red;
                }
                prevsn = row[2].ToString();

                // 차트 데이터 업데이트
                UpdateChartData(row, ref totalLoss, lineLosses);
            }
        }

        /// <summary>
        /// 메인 리스트에 행 추가
        /// </summary>
        private void AddRowToMainList(DataRow row)
        {
            dgvMainList.Rows.Add(
                row[0], row[1], row[2], row[3], row[4], 
                row[5], row[6], row[7], row[8]
            );
            dgvMainList.ClearSelection();
        }

        /// <summary>
        /// 차트 데이터 업데이트
        /// </summary>
        private void UpdateChartData(DataRow row, ref int totalLoss, int[] lineLosses)
        {
            string line = row[4].ToString().Substring(0, 1);
            string smt = row[5].ToString();

            if (!string.IsNullOrEmpty(smt))
            {
                for (int i = 0; i < dgvChartMain.ColumnCount; i++)
                {
                    if (dgvChartMain.Columns[i].HeaderText.Contains(smt))
                    {
                        // 메인 차트 업데이트
                        UpdateChartCell(dgvChartMain, 0, i, 1);
                        totalLoss++;

                        // 라인별 차트 업데이트
                        int lineIndex = int.Parse(line) - 1;
                        switch (line)
                        {
                            case "1": UpdateChartCell(dgvChartLine1, 0, i, 1); lineLosses[0]++; break;
                            case "2": UpdateChartCell(dgvChartLine2, 0, i, 1); lineLosses[1]++; break;
                            case "3": UpdateChartCell(dgvChartLine3, 0, i, 1); lineLosses[2]++; break;
                            case "4": UpdateChartCell(dgvChartLine4, 0, i, 1); lineLosses[3]++; break;
                            case "5": UpdateChartCell(dgvChartLine5, 0, i, 1); lineLosses[4]++; break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 차트 셀 값 업데이트
        /// </summary>
        private void UpdateChartCell(DataGridView dgv, int rowIndex, int colIndex, int increment)
        {
            int currentValue = int.Parse(dgv.Rows[rowIndex].Cells[colIndex].Value.ToString());
            dgv.Rows[rowIndex].Cells[colIndex].Value = currentValue + increment;
        }

        /// <summary>
        /// 불량 수량 데이터 로드
        /// </summary>
        private void LoadLossQuantityData()
        {
            string sql = $@"SELECT SUBSTR(h.created_on, 6, 5), t.line_name, COUNT(*) 
                           FROM tb_smt_cleaning_history h, tb_mes_dat_setinfo s, tb_mes_sch_daily d, 
                           tb_mes_sch_smt m, tb_mrp_std_line t 
                           WHERE CONCAT('1', SUBSTR(h.pcb_serialnumber, 2, 31), '1') = s.pcbserial 
                           AND s.dailyorder_id = d.id AND d.id = m.dailyorder_id 
                           AND SUBSTR(h.pcb_serialnumber, 1, 1) = m.step_id AND m.line_id = t.id  
                           AND h.created_on > '{dtpStart.Value:yyyy-MM-dd 00:00:00}' 
                           AND h.created_on < '{dtpEnd.Value:yyyy-MM-dd 23:59:59}' 
                           GROUP BY SUBSTR(h.created_on, 6, 5), t.line_name";

            var dataTable = ExecuteQuery(sql);
            foreach (DataRow row in dataTable.Rows)
            {
                UpdateLossQuantity(row[0].ToString(), row[1].ToString(), row[2].ToString());
            }
        }

        /// <summary>
        /// 불량 수량 업데이트
        /// </summary>
        private void UpdateLossQuantity(string date, string location, string quantity)
        {
            int lineIndex = GetLineIndex(location);
            if (lineIndex >= 0)
            {
                foreach (DataGridViewColumn column in dgvDailySum.Columns)
                {
                    if (column.Name.Contains(date))
                    {
                        int lossRowIndex = lineIndex * 5 + 2; // LOSS 행 위치
                        dgvDailySum.Rows[lossRowIndex].Cells[column.Index].Value = quantity;
                        
                        // 라인별 및 전체 합계 업데이트
                        UpdateSummaryQuantities(lineIndex, 3, int.Parse(quantity));
                    }
                }
            }
        }

        /// <summary>
        /// 생산 수량 데이터 로드
        /// </summary>
        private void LoadProductionQuantityData()
        {
            string sql = $@"SELECT SUBSTR(h.created_on, 6, 5), location, ROUND(SUM(total)) 
                           FROM tb_mes_lotid_history h, tb_mes_sch_daily a, tb_mes_std_espec e 
                           WHERE h.lot_id = a.lot_id AND a.prod_id = e.id 
                           AND (h.process_id = 1 or h.process_id = 2) AND h.event_id = 2 
                           AND h.created_on > '{dtpStart.Value:yyyy-MM-dd 00:00:00}' 
                           AND h.created_on < '{dtpEnd.Value:yyyy-MM-dd 23:59:59}' 
                           GROUP BY SUBSTR(h.created_on, 6, 5), location";

            var dataTable = ExecuteQuery(sql);
            foreach (DataRow row in dataTable.Rows)
            {
                UpdateProductionQuantity(row[0].ToString(), row[1].ToString(), row[2].ToString());
            }
        }

        /// <summary>
        /// 생산 수량 업데이트
        /// </summary>
        private void UpdateProductionQuantity(string date, string location, string quantity)
        {
            int lineIndex = GetLineIndex(location);
            if (lineIndex >= 0)
            {
                foreach (DataGridViewColumn column in dgvDailySum.Columns)
                {
                    if (column.Name.Contains(date))
                    {
                        int inRowIndex = lineIndex * 5; // IN 행 위치
                        dgvDailySum.Rows[inRowIndex].Cells[column.Index].Value = quantity;
                        
                        // 라인별 및 전체 합계 업데이트
                        UpdateSummaryQuantities(lineIndex, 1, int.Parse(quantity));
                    }
                }
            }
        }

        /// <summary>
        /// 라인 인덱스 가져오기
        /// </summary>
        private int GetLineIndex(string location)
        {
            return location switch
            {
                "1라인" or "1-LINE" => 0,
                "2라인" or "2-LINE" => 1,
                "3라인" or "3-LINE" => 2,
                "4라인" or "4-LINE" => 3,
                "5라인" or "5-LINE" => 4,
                _ => -1
            };
        }

        /// <summary>
        /// 요약 수량 업데이트
        /// </summary>
        private void UpdateSummaryQuantities(int lineIndex, int columnIndex, int quantity)
        {
            // 라인별 합계 업데이트
            int currentValue = int.Parse(dgvLineSum.Rows[lineIndex].Cells[columnIndex].Value.ToString());
            dgvLineSum.Rows[lineIndex].Cells[columnIndex].Value = currentValue + quantity;

            // 전체 합계 업데이트
            int allCurrentValue = int.Parse(dgvAllSum.Rows[0].Cells[columnIndex].Value.ToString());
            dgvAllSum.Rows[0].Cells[columnIndex].Value = allCurrentValue + quantity;
        }

        /// <summary>
        /// 요약 데이터 계산
        /// </summary>
        private void CalculateSummaryData()
        {
            CalculateDailySummary();
            CalculateLineSummary();
            CalculateAllSummary();
        }

        /// <summary>
        /// 일별 요약 계산
        /// </summary>
        private void CalculateDailySummary()
        {
            foreach (DataGridViewColumn column in dgvDailySum.Columns)
            {
                if (column.Index > 1)
                {
                    for (int i = 0; i < dgvDailySum.RowCount; i += 5)
                    {
                        CalculateDailySummaryForLine(i, column.Index);
                    }
                }
            }
        }

        /// <summary>
        /// 라인별 일일 요약 계산
        /// </summary>
        private void CalculateDailySummaryForLine(int startRowIndex, int columnIndex)
        {
            int inQty = int.Parse(dgvDailySum.Rows[startRowIndex].Cells[columnIndex].Value.ToString());
            int lossQty = int.Parse(dgvDailySum.Rows[startRowIndex + 2].Cells[columnIndex].Value.ToString());
            int outQty = inQty - lossQty;

            // OUT 수량
            dgvDailySum.Rows[startRowIndex + 1].Cells[columnIndex].Value = outQty;

            // 수율 계산
            double yield = (inQty == 0) ? 0 : 100 - ((double)lossQty / inQty) * 100;
            dgvDailySum.Rows[startRowIndex + 3].Cells[columnIndex].Value = (inQty == 0) ? "0" : $"{yield:F1}";

            // PPM 계산
            double ppm = (inQty == 0) ? 0 : ((double)lossQty / inQty) * 1000000;
            dgvDailySum.Rows[startRowIndex + 4].Cells[columnIndex].Value = (inQty == 0) ? "0" : $"{ppm:0}";
        }

        /// <summary>
        /// 라인별 요약 계산
        /// </summary>
        private void CalculateLineSummary()
        {
            for (int i = 0; i < dgvLineSum.RowCount; i++)
            {
                CalculateSummaryRow(dgvLineSum, i);
            }
        }

        /// <summary>
        /// 전체 요약 계산
        /// </summary>
        private void CalculateAllSummary()
        {
            CalculateSummaryRow(dgvAllSum, 0);
        }

        /// <summary>
        /// 요약 행 계산
        /// </summary>
        private void CalculateSummaryRow(DataGridView dgv, int rowIndex)
        {
            int inQty = int.Parse(dgv.Rows[rowIndex].Cells[1].Value.ToString());
            int lossQty = int.Parse(dgv.Rows[rowIndex].Cells[3].Value.ToString());
            int outQty = inQty - lossQty;

            dgv.Rows[rowIndex].Cells[2].Value = outQty;

            // 수율 계산
            double yield = (inQty == 0) ? 0 : 100 - ((double)lossQty / inQty) * 100;
            dgv.Rows[rowIndex].Cells[4].Value = (inQty == 0) ? "0" : $"{yield:F5}";

            // PPM 계산
            double ppm = (inQty == 0) ? 0 : ((double)lossQty / inQty) * 1000000;
            dgv.Rows[rowIndex].Cells[5].Value = (inQty == 0) ? "0" : $"{ppm:0}";
        }

        /// <summary>
        /// 차트 업데이트
        /// </summary>
        private void UpdateCharts()
        {
            // 생산 수량 합계 계산
            int totalInQty = 0;
            int[] lineInQtys = new int[5];
            
            for (int i = 0; i < 5; i++)
            {
                lineInQtys[i] = int.Parse(dgvLineSum.Rows[i].Cells[1].Value.ToString());
                totalInQty += lineInQtys[i];
            }

            // 각 차트 업데이트
            setChartAndDgv(chartMain, dgvChartMain, GetTotalLossCount(dgvChartMain), totalInQty);
            setChartAndDgv(chart1, dgvChartLine1, GetTotalLossCount(dgvChartLine1), lineInQtys[0]);
            setChartAndDgv(chart2, dgvChartLine2, GetTotalLossCount(dgvChartLine2), lineInQtys[1]);
            setChartAndDgv(chart3, dgvChartLine3, GetTotalLossCount(dgvChartLine3), lineInQtys[2]);
            setChartAndDgv(chart4, dgvChartLine4, GetTotalLossCount(dgvChartLine4), lineInQtys[3]);
            setChartAndDgv(chart5, dgvChartLine5, GetTotalLossCount(dgvChartLine5), lineInQtys[4]);
        }

        /// <summary>
        /// 총 불량 수량 가져오기
        /// </summary>
        private int GetTotalLossCount(DataGridView dgv)
        {
            int total = 0;
            for (int i = 1; i < dgv.ColumnCount; i++)
            {
                total += int.Parse(dgv.Rows[0].Cells[i].Value.ToString());
            }
            return total;
        }

        private void setChartAndDgv(Chart chart, DataGridView dgv, int lineQty, int inQty)
        {
            chart.Series["chart1"].Points.Clear();
            chart.Series["chart2"].Points.Clear();

            for (int i = 1; i < dgv.ColumnCount; i++)
            {
                // Loss 수량 차트 추가
                chart.Series["chart1"].Points.AddXY(dgv.Columns[i].HeaderText, dgv.Rows[0].Cells[i].Value.ToString());

                // 점유율 계산 및 업데이트
                int value = int.Parse(dgv.Rows[0].Cells[i].Value.ToString());
                double percentage = (value > 0 && lineQty > 0) ? ((double)value / lineQty) * 100 : 0;
                dgv.Rows[1].Cells[i].Value = (value > 0) ? $"{percentage:0.0}" : "0";
                chart.Series["chart2"].Points.AddXY(dgv.Columns[i].HeaderText, dgv.Rows[1].Cells[i].Value.ToString());

                // PPM 계산 및 업데이트
                double ppm = (value > 0 && inQty > 0) ? ((double)value / inQty) * 1000000 : 0;
                dgv.Rows[2].Cells[i].Value = (value > 0) ? $"{ppm:0}" : "0";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (ValidateInput())
            {
                InsertCleaningData();
            }
        }

        /// <summary>
        /// 입력 데이터 유효성 검증
        /// </summary>
        private bool ValidateInput()
        {
            if (string.IsNullOrEmpty(txtPcbSerial.Text) || 
                string.IsNullOrEmpty(cbTest.Text) || 
                string.IsNullOrEmpty(cbFailName.Text))
            {
                MessageBox.Show("필수 입력값을 모두 입력해주세요.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 세척 데이터 삽입
        /// </summary>
        private void InsertCleaningData()
        {
            try
            {
                EnsureConnectionOpen();
                
                string result = (cbTest.Text == "GOOD") ? "PASS" : "FAIL";
                string sql = $@"INSERT INTO tb_smt_cleaning_history 
                               (pcb_serialnumber, cleaning, result, user_id, comment) 
                               VALUES ('{txtPcbSerial.Text}', '{cbTest.Text}', '{result}', 
                               {frmMain.userID}, '{cbFailName.Text}')";
                
                ExecuteNonQuery(sql);
                GetList("INSERT");
                
                MessageBox.Show("데이터가 성공적으로 저장되었습니다.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 저장 중 오류 발생: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dgvCleaningList_DoubleClick(object sender, EventArgs e)
        {
            if (dgvMainList.CurrentRow != null && dgvMainList.CurrentCell.ColumnIndex != 1)
            {
                DeleteSelectedRecord();
            }
        }

        /// <summary>
        /// 선택된 기록 삭제
        /// </summary>
        private void DeleteSelectedRecord()
        {
            string index = dgvMainList.CurrentRow.Cells[0].Value.ToString();
            string pcbsn = dgvMainList.CurrentRow.Cells[2].Value.ToString();
            
            var result = MessageBox.Show($"{pcbsn} 를 삭제 하시겠습니까?", "Administrator", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            
            if (result == DialogResult.Yes && !string.IsNullOrEmpty(index))
            {
                try
                {
                    EnsureConnectionOpen();
                    string sql = $"DELETE FROM tb_smt_cleaning_history WHERE id = {index}";
                    ExecuteNonQuery(sql);
                    GetList("INSERT");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류 발생: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch(tabControl1.SelectedIndex)
            {
                case 0:
                    break;
                case 1:
                    dgvChartMain.ClearSelection();
                    break;
                case 2:
                    ClearChartSelections();
                    break;
                case 3:
                    ClearSummarySelections();
                    break;
            }
        }

        /// <summary>
        /// 차트 선택 해제
        /// </summary>
        private void ClearChartSelections()
        {
            dgvChartLine1.ClearSelection();
            dgvChartLine2.ClearSelection();
            dgvChartLine3.ClearSelection();
            dgvChartLine4.ClearSelection();
            dgvChartLine5.ClearSelection();
        }

        /// <summary>
        /// 요약 선택 해제
        /// </summary>
        private void ClearSummarySelections()
        {
            dgvDailySum.ClearSelection();
            dgvLineSum.ClearSelection();
            dgvAllSum.ClearSelection();
        }

        private void cbStep_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtPcbSerial.Text) && txtPcbSerial.Text.Length == PCB_SERIAL_LENGTH)
            {
                // 1st/2nd에 따라 첫 번째 문자 변경
                string prefix = (cbStep.Text == "1st") ? "1" : "2";
                txtPcbSerial.Text = prefix + txtPcbSerial.Text.Substring(1, 31);
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            GetList("SEARCH");
            tabControl1_SelectedIndexChanged(null, null);
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            UpdateModifiedRecords();
        }

        /// <summary>
        /// 수정된 기록 업데이트
        /// </summary>
        private void UpdateModifiedRecords()
        {
            try
            {
                EnsureConnectionOpen();
                int updateCount = 0;

                for (int i = 0; i < dgvMainList.RowCount; i++)
                {
                    if (dgvMainList.Rows[i].Cells[1].Style.BackColor == Color.Red)
                    {
                        string index = dgvMainList.Rows[i].Cells[0].Value.ToString();
                        string date = dgvMainList.Rows[i].Cells[1].Value.ToString();

                        if (!string.IsNullOrEmpty(index) && !string.IsNullOrEmpty(date))
                        {
                            string sql = $"UPDATE tb_smt_cleaning_history SET created_on = '{date}' WHERE id = {index}";
                            ExecuteNonQuery(sql);
                            dgvMainList.Rows[i].Cells[1].Style.BackColor = Color.White;
                            updateCount++;
                        }
                    }
                }

                if (updateCount > 0)
                {
                    MessageBox.Show($"{updateCount}개의 기록이 업데이트되었습니다.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"업데이트 중 오류 발생: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dgvMainList_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 1) // created_on 컬럼
            {
                try
                {
                    if (dgvMainList.Rows[e.RowIndex].Cells[1].Value != null &&
                        !string.IsNullOrEmpty(dgvMainList.Rows[e.RowIndex].Cells[1].Value.ToString()))
                    {
                        dgvMainList.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.Red;
                    }
                    dgvMainList.ClearSelection();
                }
                catch (Exception)
                {
                    dgvMainList.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.White;
                    dgv
