using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

public partial class frmModuleBom : Form
{
    private readonly DbHelper db;
    private string selectedModel = "";

    public frmModuleBom()
    {
        InitializeComponent();
        db = new DbHelper(Global.DbConnectionString);

        AppLogger.Info("frmModuleBom 생성자 호출");
    }

    private void frmModuleBOM_Load(object sender, EventArgs e)
    {
        AppLogger.Info("Form Load 시작");

        try
        {
            LoadSeriesList();
            LoadHoldList();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Form Load 오류", ex);
            MessageBox.Show(ex.Message);
        }

        AppLogger.Info("Form Load 완료");
    }

    //===============================
    // 1. 시리즈 목록
    //===============================
    private void LoadSeriesList()
    {
        AppLogger.Info("시리즈 목록 조회");

        string sql = @"
            SELECT DISTINCT series 
            FROM tb_mes_std_espec
            WHERE espec_flag = 'R'
            ORDER BY series";

        var dt = db.ExecuteQuery(sql);
        cbSeries.Items.Clear();

        foreach (DataRow row in dt.Rows)
            cbSeries.Items.Add(row["series"].ToString());
    }

    //===============================
    // 2. 모델 목록
    //===============================
    private void cbSeries_SelectedIndexChanged(object sender, EventArgs e)
    {
        AppLogger.Info($"시리즈 선택: {cbSeries.Text}");

        LoadModelList(cbSeries.Text);
    }

    private void LoadModelList(string series)
    {
        string sql = @"
            SELECT prod_code 
            FROM tb_mes_std_espec
            WHERE series = @series
            ORDER BY prod_code";

        var dt = db.ExecuteQuery(sql,
            new MySqlParameter("@series", series));

        cbModel.Items.Clear();

        foreach (DataRow row in dt.Rows)
            cbModel.Items.Add(row["prod_code"].ToString());
    }

    //===============================
    // 3. 모델 선택 → BOM 전체 처리
    //===============================
    private void cbModel_SelectedIndexChanged(object sender, EventArgs e)
    {
        selectedModel = cbModel.Text;

        AppLogger.Info($"모델 선택: {selectedModel}");

        if (string.IsNullOrWhiteSpace(selectedModel))
            return;

        var sw = Stopwatch.StartNew();

        try
        {
            ClearUI();

            LoadBomHeader();
            var bomList = LoadBomComponents();
            var inv = LoadBulkInventory(bomList);
            RenderInventory(inv);
            LoadPcbMaterial();
        }
        catch (Exception ex)
        {
            AppLogger.Error("모델 선택 처리 오류", ex);
            MessageBox.Show(ex.Message);
        }

        AppLogger.Info($"cbModel_SelectedIndexChanged 완료: {sw.ElapsedMilliseconds}ms");
    }

    //===============================
    // 4. BOM Header 조회
    //===============================
    private void LoadBomHeader()
    {
        string sql = @"
            SELECT prod_code, model_name, series
            FROM tb_mes_std_espec
            WHERE prod_code = @code";

        var dt = db.ExecuteQuery(sql,
            new MySqlParameter("@code", selectedModel));

        if (dt.Rows.Count == 0)
        {
            AppLogger.Warn($"BOM Header 없음: {selectedModel}");
            return;
        }

        lblModelName.Text = dt.Rows[0]["model_name"].ToString();
    }

    //===============================
    // 5. 구성품 조회
    //===============================
    private DataTable LoadBomComponents()
    {
        string sql = @"
            SELECT item_code, item_name
            FROM tb_mes_bom
            WHERE parent_code = @code
            ORDER BY seq";

        var dt = db.ExecuteQuery(sql,
            new MySqlParameter("@code", selectedModel));

        dgvCompList.DataSource = dt;

        AppLogger.Info($"BOM 구성품 {dt.Rows.Count}건 조회됨");

        return dt;
    }

    //===============================
    // 6. 재고 조회 (Bulk)
    //===============================
    private DataTable LoadBulkInventory(DataTable comps)
    {
        if (comps.Rows.Count == 0) return new DataTable();

        var list = comps.AsEnumerable()
                        .Select(r => r["item_code"].ToString())
                        .ToArray();

        string inCodes = string.Join(",", list.Select(x => $"'{x}'"));

        string sql = $@"
            SELECT prod_code, SUM(inventory) AS qty
            FROM tb_in_wafer_info
            WHERE prod_code IN ({inCodes})
            GROUP BY prod_code";

        var dt = db.ExecuteQuery(sql);

        AppLogger.Info($"Bulk Inventory 조회 {dt.Rows.Count}건");

        return dt;
    }

    private void RenderInventory(DataTable inv)
    {
        dgvInventory.DataSource = inv;
    }

    //===============================
    // 7. PCB 자재 (LA41*)
    //===============================
    private void LoadPcbMaterial()
    {
        string sql = @"
            SELECT prod_code, SUM(inventory) qty
            FROM tb_in_wafer_info
            WHERE prod_code LIKE 'LA41%'
            GROUP BY prod_code";

        dgvPCB.DataSource = db.ExecuteQuery(sql);
    }

    //===============================
    // 8. Hold 리스트
    //===============================
    private void LoadHoldList()
    {
        string sql = @"
            SELECT prod_code, hold_reason, reg_date
            FROM tb_mes_hold
            WHERE del_flag = 'N'
            ORDER BY reg_date DESC";

        dgvHold.DataSource = db.ExecuteQuery(sql);
    }

    //===============================
    // 9. UI 초기화
    //===============================
    private void ClearUI()
    {
        dgvCompList.DataSource = null;
        dgvInventory.DataSource = null;
        dgvPCB.DataSource = null;
    }
}
