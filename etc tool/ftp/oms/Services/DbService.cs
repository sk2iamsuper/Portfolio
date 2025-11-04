using System;
cmd.ExecuteNonQuery();
}

public DataTable ExecuteQuery(string sql)
{
using var da = new MySqlDataAdapter(sql, _conn);
var dt = new DataTable();
da.Fill(dt);
return dt;
}

// Example of insert using parameterized query to avoid SQL injection
public void InsertWipInfo(CMWipInf model)
{
var sql = @"INSERT INTO tb_ufd_input_info
(start_tag, company_code, prod_code, run_id, lot_id, lot_type, return_type, process_id,
step_id, step_seq_no, step_desc, step_in_dttm, area_flag, area_id, chip_qty, wafer_qty,
wafer_chip_flag, hold_flag, hold_code, hold_dttm, ncf_code, nca_code, nct_code, ncq_code,
other, loss_qty, bonus_qty, fab_line, create_dttm, cutoff_date, inkless)
VALUES
(@start_tag,@company_code,@prod_code,@run_id,@lot_id,@lot_type,@return_type,@process_id,
@step_id,@step_seq_no,@step_desc,@step_in_dttm,@area_flag,@area_id,@chip_qty,@wafer_qty,
@wafer_chip_flag,@hold_flag,@hold_code,@hold_dttm,@ncf_code,@nca_code,@nct_code,@ncq_code,
@other,@loss_qty,@bonus_qty,@fab_line,@create_dttm,@cutoff_date,@inkless);";

using var cmd = new MySqlCommand(sql, _conn);
cmd.Parameters.AddWithValue("@start_tag", model.StartTag);
cmd.Parameters.AddWithValue("@company_code", model.CompanyCode);
cmd.Parameters.AddWithValue("@prod_code", model.ProductCode);
cmd.Parameters.AddWithValue("@run_id", model.RunId);
cmd.Parameters.AddWithValue("@lot_id", model.LotId);
cmd.Parameters.AddWithValue("@lot_type", model.LotType);
cmd.Parameters.AddWithValue("@return_type", model.ReturnType);
cmd.Parameters.AddWithValue("@process_id", model.ProcessId);
cmd.Parameters.AddWithValue("@step_id", model.StepId);
cmd.Parameters.AddWithValue("@step_seq_no", model.StepSeqNo);
cmd.Parameters.AddWithValue("@step_desc", model.StepDesc);
cmd.Parameters.AddWithValue("@step_in_dttm", model.StepInDttm);
cmd.Parameters.AddWithValue("@area_flag", model.AreaFlag);
cmd.Parameters.AddWithValue("@area_id", model.AreaId);
cmd.Parameters.AddWithValue("@chip_qty", model.ChipQty);
cmd.Parameters.AddWithValue("@wafer_qty", model.WaferQty);
cmd.Parameters.AddWithValue("@wafer_chip_flag", "");
cmd.Parameters.AddWithValue("@hold_flag", model.HoldFlag);
cmd.Parameters.AddWithValue("@hold_code", model.HoldCode);
cmd.Parameters.AddWithValue("@hold_dttm", model.HoldDttm);
cmd.Parameters.AddWithValue("@ncf_code", model.NcfCode);
cmd.Parameters.AddWithValue("@nca_code", model.NcaCode);
cmd.Parameters.AddWithValue("@nct_code", model.NctCode);
cmd.Parameters.AddWithValue("@ncq_code", model.NcqCode);
cmd.Parameters.AddWithValue("@other", model.Other);
cmd.Parameters.AddWithValue("@loss_qty", model.LossQty);
cmd.Parameters.AddWithValue("@bonus_qty", model.BonusQty);
cmd.Parameters.AddWithValue("@fab_line", model.FabLine);
cmd.Parameters.AddWithValue("@create_dttm", model.CreateDttm);
cmd.Parameters.AddWithValue("@cutoff_date", model.CutoffDate);
cmd.Parameters.AddWithValue("@inkless", model.Inkless);

cmd.ExecuteNonQuery();
}

public void Dispose()
{
try { _conn?.Close(); } catch { }
_conn?.Dispose();
}
}
}
