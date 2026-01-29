using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;

namespace MES_WMS
{
    public partial class User : Form
    {
        private string userFactory = UserCommon.Public_Function.user_Factory;
        private static string serverName = UserCommon.Public_Function.user_Server;
        UserCommon.CmCn connectionWithServer = new UserCommon.CmCn(serverName, "cmv");
        UserCommon.CmCn connection = new UserCommon.CmCn();
        public event ChildFromEventHandler OnNotifyParent;

        public User()
        {
            InitializeComponent();
            InitializeForm();
        }

        private void InitializeForm()
        {
            textBoxEmployeeNumber.Text = "";
            textBoxPassword.Text = "";
        }

        public User(string employeeNumber)
        {
            InitializeComponent();
            InitializeForm();

            textBoxEmployeeNumber.Text = employeeNumber;
            textBoxPassword.Text = "";
            textBoxPassword.Focus();
        }

        /// <summary>
        /// 사원 비밀번호 조회 - 필요
        /// </summary>
        /// <param name="employeeNumber">사원번호</param>
        /// <returns>비밀번호</returns>
        public string GetEmployeePassword(string employeeNumber)
        {
            string query = "";
            string password = "";
            
            // 필수 로직: 사원 비밀번호 조회 쿼리
            query = ""
                + "\r\n" + "SELECT ISNULL(a.password,'') AS password "
                + "\r\n" + "FROM thb01 a, thb02 b"
                + "\r\n" + "WHERE a.saup_gubn = b.saup_gubn"  // 사업구분
                + "\r\n" + "AND a.dept_code = b.dept_code"    // 부서코드
                + "\r\n" + "AND a.goout_gubn = '1'"           // 퇴사구분 (1: 재직)
                + "\r\n" + "AND a.empno = '" + employeeNumber + "'"
                + "\r\n" + "AND ISNULL(a.password,'') = '" + textBoxPassword.Text + "'"
                + "\r\n" + "ORDER BY a.empno";

            DataSet dataSet = connectionWithServer.ResultReturnDataSet(query);
            if (dataSet.Tables[0].Rows.Count > 0)
            {
                password = dataSet.Tables[0].Rows[0][0].ToString();
            }

            return password;
        }

        private void buttonSet_Click(object sender, EventArgs e)
        {
            string query = string.Empty;
            
            if (!string.IsNullOrWhiteSpace(textBoxPassword.Text))
            {
                // 필수 로직: 사용자 인증 쿼리
                query = ""
                    + "\r\n" + "SELECT a.password, a.saup_gubn, a.dept_code, b.dept_name, a.empno, a.kname, a.jikmu"
                    + "\r\n" + "FROM thb01 a, thb02 b"
                    + "\r\n" + "WHERE a.saup_gubn = b.saup_gubn"  // 사업구분
                    + "\r\n" + "AND a.dept_code = b.dept_code"    // 부서코드
                    + "\r\n" + "AND a.goout_gubn = '1'"           // 퇴사구분 (1: 재직)
                    + "\r\n" + "AND a.empno = '" + textBoxEmployeeNumber.Text + "'"
                    + "\r\n" + "AND ISNULL(a.password,'') = '" + textBoxPassword.Text + "'"
                    + "\r\n" + "ORDER BY a.empno";

                DataSet userCheck = connectionWithServer.ResultReturnDataSet(query);
                
                // 필수 로직: 인증 성공 여부 확인
                if (userCheck.Tables[0].Rows.Count > 0)
                {
                    // 인증 성공
                    this.Close();
                }
                else
                {
                    userCheck.Dispose();
                    userCheck = null;

                    MessageBox.Show("비밀번호가 잘못되었습니다!");
                    textBoxPassword.Text = "";
                    textBoxPassword.Focus();
                }
            }
            else
            {
                MessageBox.Show("비밀번호를 입력해주세요!");
                textBoxPassword.Text = "";
                textBoxPassword.Focus();
            }
        }
    }
}
