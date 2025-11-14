using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mes_
{
    public partial class frm모듈BOM : Form
    {
        private readonly MySqlConnection _connection;

        public frm모듈BOM(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
        }

        #region Form Load & 초기화

        private async void frm모듈BOM_Load(object sender, EventArgs e)
        {
            if (frmMain.dbsite.Contains("VPV"))
                txtFabinfo.Text = "* FAB Info.  M (M/L), S (P/B)";

            // 비동기 초기 로드
            await Task.WhenAll(LoadSeriesAsync(), LoadNandSeriesAsync(), LoadMhHoldListAsync());
        }

        #endregion

        #region DB 헬퍼 (비동기, 파라미터화)

        private Task<DataTable> ExecuteDataTableAsync(string sql, params MySqlParameter[] parameters)
        {
            // DataAdapter.Fill 은 blocking 이므로 Task.Run으로 오프로드
            return Task.Run(() =>
            {
                var dt = new DataTable();
                using (var cmd = new MySqlCommand(sql, _connection))
                {
                    if (parameters != null && parameters.Length > 0)
                        cmd.Parameters.AddRange(parameters);

                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
                return dt;
            });
        }

        private Task<int> ExecuteScalarIntAsync(string sql, params MySqlParameter[] parameters)
        {
            return Task.Run(() =>
            {
                using (var cmd = new MySqlCommand(sql, _connection))
                {
                    if (parameters != null && parameters.Length > 0)
                        cmd.Parameters.AddRange(parameters);
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value) return 0;
                    return Convert.ToInt32(result);
                }
            });
        }

        #endregion

        #region 초기 데이터 로드 (Series, NAND Series, MH Hold)

        private async Task LoadSeriesAsync()
        {
            cbSeries.Items.Clear();
            var sql = "SELECT DISTINCT series FROM tb_mes_std_espec WHERE espec_flag = 'R' AND series IS NOT NULL ORDER BY series";
            var dt = await ExecuteDataTableAsync(sql);
            foreach (DataRow r in dt.Rows)
                cbSeries.Items.Add(r[0].ToString());
        }

        private async Task LoadNandSeriesAsync()
        {
            cbK49.Items.Clear();
            var sql = "SELECT DISTINCT SUBSTR(SALES_CODE, 1, 2) FROM tb_in_wafer_info WHERE SALES_CODE != '-'";
            var dt = await ExecuteDataTableAsync(sql);
            foreach (DataRow r in dt.Rows)
                cbK49.Items.Add(r[0].ToString());
        }

        private async Task LoadMhHoldListAsync()
        {
            dgvMHHold.Rows.Clear();
            var sql = @"SELECT DATE_FORMAT(created_on, '%Y-%m-%d %H:%i:%s') AS created_on, prod, del_flag, hold_memo, 
                               (SELECT user_id FROM tb_user WHERE id = n.user_id) AS user_id, n.id 
                        FROM tb_mh_control_n n WHERE del_flag = 'N'";
            var dt = await ExecuteDataTableAsync(sql);
            foreach (DataRow r in dt.Rows)
            {
                // 순서: id, created_on, del_flag, hold_memo, user_id, prod (원래 코드와 순서 맞춤)
                dgvMHHold.Rows.Add(r["id"].ToString(), r["created_on"].ToString(), r["del_flag"].ToString(), r["hold_memo"].ToString(), r["user_id"].ToString(), r["prod"].ToString());
            }

            if (dgvMHHold.Rows.Count > 0)
                dgvMHHold.ClearSelection();
        }

        #endregion

        #region ComboBox 관련 - 모델/ NAND 목록 로드

        private async void cbSeries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cbSeries.Text)) return;
            await LoadModelListAsync(cbSeries.Text);
        }

        private async Task LoadModelListAsync(string series)
        {
            cbModel.Items.Clear();

            // prod_code를 기준으로 앞 부분이 동일한 것들은 하나만 보여주기 위해 DB에서 정렬하고 로직에서 중복 제거
            var sql = "SELECT prod_code FROM tb_mes_std_espec WHERE series = @series AND espec_flag = 'R' ORDER BY prod_code DESC";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@series", series));

            string prevKey = null;
            foreach (DataRow r in dt.Rows)
            {
                var code = r["prod_code"].ToString();
                var key = code.Length >= 21 ? code.Substring(0, 21) : code;
                if (prevKey != key)
                {
                    cbModel.Items.Add(code);
                    prevKey = key;
                }
            }
        }

        private async void cbK49_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cbK49.Text)) return;
            await LoadNandListAsync(cbK49.Text);
        }

        private async Task LoadNandListAsync(string prefix2)
        {
            cbNandProd.Items.Clear();
            var sql = "SELECT DISTINCT SALES_CODE FROM tb_in_wafer_info WHERE SALES_CODE LIKE @pfx";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@pfx", prefix2 + "%"));
            foreach (DataRow r in dt.Rows)
                cbNandProd.Items.Add(r[0].ToString());
        }

        private async void cbNandProd_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cbNandProd.Text)) return;

            dgvList.Rows.Clear();
            dgvPCBList.Rows.Clear();
            dataGridView1.Rows.Clear();
            dgvException2.Rows.Clear();
            dataGridView2.Rows.Clear();

            await LoadNandProdListAsync(cbNandProd.Text);
        }

        private async Task LoadNandProdListAsync(string compCode)
        {
            cbModel.Text = null;
            cbModel.Items.Clear();

            var sql = @"SELECT e.prod_code 
                        FROM SEC_MODSTBOM b
                        JOIN tb_mes_std_espec e ON b.prod_code = e.prod_code
                        WHERE b.flag = 'R' AND b.comp_code = @comp";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@comp", compCode));
            foreach (DataRow r in dt.Rows)
                cbModel.Items.Add(r[0].ToString());
        }

        #endregion

        #region 모델 선택 처리 - 리팩토링된 흐름

        private async void cbModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cbModel.Text)) return;

            try
            {
                // UI 초기화
                txtOptionCode.Text = string.Empty;
                dgvException2.Rows.Clear();
                dgvList.Rows.Clear();
                dataGridView2.Rows.Clear();
                dataGridView1.Rows.Clear();
                dgvPCBList.Rows.Clear();

                // 1) BOM 헤더 로드
                await LoadBomHeaderAsync(cbModel.Text);

                // 2) 구성품 리스트 로드 (dataGridView2)
                var comps = await LoadComponentListAsync(cbModel.Text);

                // 3) 구성품들의 재고(wafer) 정보를 한번에 조회
                var inventoryRows = await LoadInventoryBulkAsync(comps);

                // 4) 화면에 렌더링(색상 적용 포함)
                RenderInventory(inventoryRows);

                // 5) PCB 관련 자재 조회
                await LoadPcbMaterialsAsync(cbModel.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region 세부 로직: BOM 헤더, 컴포넌트, 인벤토리, 렌더링

        private async Task LoadBomHeaderAsync(string prodCode)
        {
            // SEC_MODSTBOM에서 prod_code 및 앞 16자(원래 코드에서 사용)를 가져와 dgvList에 표시
            var sql = "SELECT DISTINCT prod_code, SUBSTR(prod_code,1,16) AS short_code FROM SEC_MODSTBOM WHERE prod_code = @prod AND flag = 'R' ORDER BY prod_code";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@prod", prodCode));
            foreach (DataRow r in dt.Rows)
            {
                dgvList.Rows.Add(dgvList.RowCount + 1, r["prod_code"].ToString(), "", r["short_code"].ToString());
            }

            if (dgvList.Rows.Count > 0) dgvList.ClearSelection();
        }

        /// <summary>
        /// dataGridView2에 PCB 구성품(comp_code, pcb_seq, comp_qty, front_qty, back_qty 등)을 채우고
        /// 구성품 코드 목록을 반환한다.
        /// </summary>
        private async Task<List<string>> LoadComponentListAsync(string prodCode)
        {
            var comps = new List<string>();
            var sql = @"SELECT comp_code, pcb_seq, comp_qty, front_qty, back_qty
                        FROM SEC_MODSTBOM
                        WHERE prod_code = @prod AND flag = 'R'
                        ORDER BY prod_code";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@prod", prodCode));

            foreach (DataRow r in dt.Rows)
            {
                var comp = r["comp_code"].ToString();
                // dataGridView2에 중복 없이 추가 (원래 코드 로직 유지)
                if (!comps.Contains(comp))
                {
                    dataGridView2.Rows.Add(dataGridView2.RowCount + 1,
                                           comp,
                                           r["pcb_seq"].ToString(),
                                           "", // col4 placeholder
                                           r["comp_qty"].ToString(),
                                           r["front_qty"].ToString(),
                                           r["back_qty"].ToString(),
                                           ""); // col7 placeholder
                    comps.Add(comp);
                }
            }

            if (dataGridView2.Rows.Count > 0) dataGridView2.ClearSelection();
            return comps;
        }

        /// <summary>
        /// 구성품 리스트를 받아 tb_in_wafer_info에서 전체를 한 번에 조회해 결과 DataTable 반환
        /// (기존 반복 쿼리 대신 IN절 사용)
        /// </summary>
        private async Task<DataTable> LoadInventoryBulkAsync(List<string> compCodes)
        {
            // compCodes 가 비어있으면 빈 테이블 반환
            var dtEmpty = new DataTable();
            if (compCodes == null || compCodes.Count == 0) return dtEmpty;

            // prod_code like 'comp%' 조건을 OR로 묶는 형태로 단일 쿼리 실행
            // 성능을 위해 Prepared LIKE 조건을 여러 파라미터로 추가
            var whereClauses = new List<string>();
            var parameters = new List<MySqlParameter>();
            for (int i = 0; i < compCodes.Count; i++)
            {
                string pName = $"@p{i}";
                whereClauses.Add($"i.prod_code LIKE {pName}");
                parameters.Add(new MySqlParameter(pName, compCodes[i] + "%"));
            }

            var sql = $@"
                SELECT i.prod_code, i.sale_option, SUM(i.inventory) AS inventory_sum, i.fab_line, i.lot_type,
                       (SELECT lot_id FROM tb_in_wafer_info WHERE prod_code = i.prod_code AND flag IS NULL AND sample = 'N' AND inventory != 0 ORDER BY work_week LIMIT 1) AS lot_id,
                       (SELECT work_week FROM tb_in_wafer_info WHERE prod_code = i.prod_code AND flag IS NULL AND sample = 'N' AND inventory != 0 ORDER BY work_week LIMIT 1) AS work_week
                FROM tb_in_wafer_info i
                WHERE (" + string.Join(" OR ", whereClauses) + @") AND flag IS NULL AND sample = 'N' AND inventory != 0
                GROUP BY i.prod_code, i.sale_option, i.fab_line, i.lot_type
                ORDER BY i.prod_code, i.sale_option";
            var dt = await ExecuteDataTableAsync(sql, parameters.ToArray());
            return dt;
        }

        /// <summary>
        /// inventory DataTable을 받아 dataGridView1에 추가하고 색상/예외 처리 수행
        /// </summary>
        private void RenderInventory(DataTable inventoryDt)
        {
            if (inventoryDt == null || inventoryDt.Rows.Count == 0)
            {
                dataGridView1.ClearSelection();
                return;
            }

            // 기존 로직: K9/KL/K4 구분, 색상 칠하기, isExceptionRole 호출
            foreach (DataRow r in inventoryDt.Rows)
            {
                var prodCode = r["prod_code"].ToString();
                var saleOption = r["sale_option"].ToString();
                var lotType = r["lot_type"].ToString();
                var fabLine = r["fab_line"].ToString();
                var lotId = r["lot_id"]?.ToString() ?? "";
                var workWeek = r["work_week"]?.ToString() ?? "";

                // 안전하게 길이 체크
                var prefix2 = prodCode.Length >= 2 ? prodCode.Substring(0, 2) : prodCode;
                var kpartProdCode = prodCode.Length >= 18 ? prodCode.Substring(0, 18) : prodCode;
                var mpartOptionChar = saleOption.Length >= 2 ? saleOption.Substring(1, 1) : "";

                // 기본 행 추가 (원래 컬럼 순서: idx, prod_code, sale_option, lot_type, fab_line, lot_id, work_week)
                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, prodCode, saleOption, lotType, fabLine, lotId, workWeek);
                var rowIdx = dataGridView1.RowCount - 1;

                // 기본 색상
                Color baseColor = Color.SkyBlue;
                if (prefix2 == "K9" || prefix2 == "KL") baseColor = Color.LightPink;
                else if (prefix2 == "K4") baseColor = Color.LightPink;

                SetRowBackColor(dataGridView1.Rows[rowIdx], baseColor);

                // 예외 검사
                var exceptionRole = isExceptionRole(kpartProdCode, GetFabCharFromProd(prodCode), mpartOptionChar, cbModel.Text);
                if (prefix2 == "K9" || prefix2 == "KL")
                {
                    if (exceptionRole == "TRUE")
                    {
                        // 추가 세부 규칙: cbModel.Text 길이에 따른 판단
                        TryApplyGreenForK9KL(dataGridView1.Rows[rowIdx], prodCode, lotType, cbModel.Text);
                    }
                }
                else if (prefix2 == "K4")
                {
                    if (exceptionRole == "TRUE")
                    {
                        SetRowBackColor(dataGridView1.Rows[rowIdx], Color.YellowGreen);
                    }
                }
                else
                {
                    // non-K family -> leave SkyBlue (already set)
                }
            }

            dataGridView1.ClearSelection();
        }

        private string GetFabCharFromProd(string prodCode)
        {
            // 원래 코드에서 row[0].ToString().Substring(19,1) 사용 -> 안전하게 처리
            if (prodCode.Length > 19) return prodCode.Substring(19, 1);
            if (prodCode.Length > 18) return prodCode.Substring(prodCode.Length - 1, 1);
            return string.Empty;
        }

        private void TryApplyGreenForK9KL(DataGridViewRow row, string prodCode, string lotType, string modelCode)
        {
            // 원본 로직을 유지하되 안전한 인덱스 검사 적용
            if (string.IsNullOrEmpty(modelCode)) return;

            if (modelCode.Length == 22)
            {
                var modelChar19 = modelCode.Length > 19 ? modelCode.Substring(19, 1) : "";
                // lotType == 'C' or 'H' and modelChar19 == 'P'
                if ((lotType == "C" || lotType == "H") && modelChar19 == "P")
                {
                    SetRowBackColor(row, Color.LawnGreen);
                }
                else if ((lotType == "M" || lotType == "L") && (modelChar19 == "Q" || modelChar19 == "M"))
                {
                    SetRowBackColor(row, Color.LawnGreen);
                }
                else if ((lotType == "P" || lotType == "B") && (modelChar19 == "V" || modelChar19 == "S"))
                {
                    SetRowBackColor(row, Color.LawnGreen);
                }
            }
            else if (modelCode.Length == 25)
            {
                // compare substring(23,2) with lotType
                if (modelCode.Length >= 25)
                {
                    var cmp = modelCode.Substring(23, 2);
                    if (cmp == lotType)
                        SetRowBackColor(row, Color.LawnGreen);
                }
            }
        }

        private void SetRowBackColor(DataGridViewRow row, Color color)
        {
            for (int c = 0; c < row.Cells.Count; c++)
                row.Cells[c].Style.BackColor = color;
        }

        #endregion

        #region PCB 자재(예: LA41) 조회

        private async Task LoadPcbMaterialsAsync(string mProdCode)
        {
            dgvPCBList.Rows.Clear();

            // SEC_MATSTBOM에서 LA41* 자재 조회 (원본 로직)
            var sql = @"SELECT piece_part_no FROM SEC_MATSTBOM WHERE prod_code = @prod AND flag = 'R' AND piece_part_no LIKE 'LA41%'";
            var dt = await ExecuteDataTableAsync(sql, new MySqlParameter("@prod", mProdCode));

            string matcode = string.Empty;
            if (dt.Rows.Count > 0)
                matcode = dt.Rows[0]["piece_part_no"].ToString();

            if (string.IsNullOrEmpty(matcode))
            {
                dgvPCBList.ClearSelection();
                return;
            }

            // mat_ver 별로 vendor 및 lot_qty 합계 조회 (한 번의 쿼리)
            var sql2 = @"SELECT a.vendor, l.mat_ver, SUM(l.lot_qty) AS qty
                         FROM tb_material_lotid l
                         JOIN tb_mrp_std_avl a ON l.avl_id = a.id
                         WHERE l.lotid LIKE @lotid AND (l.work_flag IS NULL OR l.work_flag = 'L')
                         GROUP BY a.vendor, l.mat_ver";
            var dt2 = await ExecuteDataTableAsync(sql2, new MySqlParameter("@lotid", $"V{matcode}%"));

            int sum = 0;
            foreach (DataRow r in dt2.Rows)
            {
                var vendor = r["vendor"].ToString();
                var ver = r["mat_ver"].ToString();
                var qty = Convert.ToInt32(r["qty"]);
                dgvPCBList.Rows.Add(dgvPCBList.RowCount + 1, matcode, vendor, ver, qty);
                sum += qty;

                // Series/Model 조건에 따른 색상/글자색 적용 (원본 로직)
                if (cbSeries.Text == "PSSD T7 SHIELD" && mProdCode.Length >= 18 && mProdCode.Substring(16, 2) == "YY")
                {
                    if (ver == "005")
                        SetRowCellBackColor(dgvPCBList.Rows[dgvPCBList.RowCount - 1], Color.LawnGreen);
                    else
                        SetRowCellForeColor(dgvPCBList.Rows[dgvPCBList.RowCount - 1], Color.Red);
                }
                else if (cbSeries.Text == "PSSD T7 SHIELD" && (mProdCode.Length < 18 || mProdCode.Substring(16, 2) != "YY"))
                {
                    if (ver == "005")
                        SetRowCellForeColor(dgvPCBList.Rows[dgvPCBList.RowCount - 1], Color.Red);
                    else
                        SetRowCellBackColor(dgvPCBList.Rows[dgvPCBList.RowCount - 1], Color.LawnGreen);
                }
            }

            // 합계 행 추가
            dgvPCBList.Rows.Add("", "", "-", "-", sum);
            dgvPCBList.ClearSelection();
        }

        private void SetRowCellBackColor(DataGridViewRow row, Color color)
        {
            for (int i = 0; i < row.Cells.Count; i++)
                row.Cells[i].Style.BackColor = color;
        }

        private void SetRowCellForeColor(DataGridViewRow row, Color color)
        {
            for (int i = 0; i < row.Cells.Count; i++)
                row.Cells[i].Style.ForeColor = color;
        }

        #endregion

        #region 예외 규칙 검사 (isExceptionRole, opt2) - 로직 분리

        private string isExceptionRole(string kpart_prod_code, string fab, string option2, string mpart_prod_code)
        {
            // 안전한 파라미터화
            var sql = @"SELECT DISTINCT prod_code FROM tb_in_opt2_n 
                        WHERE gam_except_flag = 'Y' AND del_flag = 'N' AND feb = @fab AND option2 = @opt2";
            var dt = ExecuteDataTableAsync(sql, new MySqlParameter("@fab", fab), new MySqlParameter("@opt2", option2)).Result;

            foreach (DataRow row in dt.Rows)
            {
                var excepProdCode = row["prod_code"].ToString();
                if (IsPatternMatch(excepProdCode, kpart_prod_code))
                {
                    txtException2.Text = excepProdCode;

                    // 관련 제약 조건 정보를 조회하여 opt2 검사
                    var sql2 = @"SELECT gam_prod, gam_cond, id, update_date, feb, option2 
                                 FROM tb_in_opt2_n 
                                 WHERE gam_except_flag = 'Y' AND (del_flag = 'N' OR del_flag IS NULL) 
                                   AND feb = @fab AND option2 = @opt2 AND prod_code = @pcode";
                    var dt2 = ExecuteDataTableAsync(sql2,
                        new MySqlParameter("@fab", fab),
                        new MySqlParameter("@opt2", option2),
                        new MySqlParameter("@pcode", excepProdCode)).Result;

                    foreach (DataRow sub in dt2.Rows)
                    {
                        var gamProd = sub["gam_prod"].ToString();
                        var gamCond = sub["gam_cond"].ToString();
                        var id = sub["id"].ToString();
                        var updateDate = sub["update_date"].ToString();
                        var _option2 = sub["option2"].ToString();
                        var _feb = sub["feb"].ToString();

                        var result = opt2(mpart_prod_code, gamProd, gamCond);
                        if (!result)
                        {
                            dgvException2.Rows.Add(id, excepProdCode, gamProd, gamCond, updateDate, _option2, _feb);
                            dgvException2.ClearSelection();
                            return "FALSE";
                        }
                    }
                }
            }

            return "TRUE";
        }

        /// <summary>
        /// 패턴 매칭: excep_prod_code의 '_'는 와일드카드로 취급
        /// </summary>
        private bool IsPatternMatch(string pattern, string target)
        {
            if (pattern == null || target == null) return false;
            int len = Math.Min(pattern.Length, target.Length);
            for (int i = 0; i < len; i++)
            {
                var p = pattern.Substring(i, 1);
                if (p == "_") continue;
                if (p != target.Substring(i, 1)) return false;
            }
            // if pattern longer than target, we consider mismatch
            if (pattern.Length > target.Length) return false;
            return true;
        }

        private bool opt2(string mpart_prod_code, string gam_prod, string gam_cond)
        {
            if (gam_cond == "=")
            {
                return PatternEquals(gam_prod, mpart_prod_code);
            }
            else if (gam_cond == "!=")
            {
                return !PatternEquals(gam_prod, mpart_prod_code);
            }
            else
            {
                MessageBox.Show("제약조건에 확인이 필요한 기호가 존재합니다.");
                return false;
            }
        }

        /// <summary>
        /// gam_prod의 '_'는 와일드카드. 모든 비언더스코어 문자는 동일해야 함.
        /// </summary>
        private bool PatternEquals(string pattern, string target)
        {
            if (pattern == null || target == null) return false;
            if (pattern.Length > target.Length) return false; // 길이 불일치 시 false

            for (int i = 0; i < pattern.Length; i++)
            {
                var p = pattern.Substring(i, 1);
                if (p == "_") continue;
                if (p != target.Substring(i, 1)) return false;
            }
            return true;
        }

        #endregion

        #region 데이터그리드 더블클릭 등 이벤트

        private void dataGridView1_DoubleClick(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null) return;

            dgvException2.Rows.Clear();

            var component_code = dataGridView1.CurrentRow.Cells[1].Value?.ToString() ?? "";
            var component_option = dataGridView1.CurrentRow.Cells[2].Value?.ToString() ?? "";
            txtOptionCode.Text = component_option;

            // isExceptionRole 자체가 블로킹 DB 호출을 포함하므로 Task.Run으로 오프로드
            Task.Run(() =>
            {
                var result = isExceptionRole(
                    component_code.Length >= 18 ? component_code.Substring(0, 18) : component_code,
                    component_code.Length > 19 ? component_code.Substring(19, 1) : "",
                    component_option.Length > 1 ? component_option.Substring(1, 1) : "",
                    cbModel.Text);
                // 필요시 UI 업데이트는 Invoke 필요
            });
        }

        #endregion
    }
}
