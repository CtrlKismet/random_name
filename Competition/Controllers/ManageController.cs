﻿using Competition.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Competition.Controllers
{
    public class ManageController : Controller
    {
        // GET: Manage
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// 报名参加比赛
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        [Authorize]
        public ActionResult Register(int? ID)
        {
            if (!ID.HasValue) return RedirectToAction("Index", "Home");
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            competition c = msgBal.GetCompetitionByID(ID.Value);
            return View(c);
        }

        /// <summary>
        /// 验证队伍信息
        /// </summary>
        /// <param name="tmp">队伍人员</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public ActionResult Register(TemplateTeam tmp)
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            competition c = msgBal.GetCompetitionByID(tmp.number);
            string url = "/Manage/Register?ID=" + tmp.number.ToString();

            //已经超过报名截止时间
            if (DateTime.Now.Date > c.EndTime.Date)
            {
                return View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "已经超过报名时间！", Url = url });
            }

            team t = new team();

            List<student> templateStudents = new List<student>();
            bool[] vis = new bool[5];
            int group = 0, count = 0;

            //将表格信息转化为队伍信息
            foreach (string m in tmp.member)
            {
                if (m == "" || m == null) continue;
                t.Member += m + "&";
                student s = msgBal.GetStudentByID(m);
                if (s == null)
                {
                    return View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "用户 \"" + m + "\" 不存在！", Url = url });
                }

                string errorMsg = msgBal.CanRegisterToCompetition(s, c);
                if (errorMsg != null)
                {
                    return View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "用户 \"" + m + "\" " + errorMsg, Url = url });
                }

                if (templateStudents.FirstOrDefault(x => x.StudentID == s.StudentID) != null)
                {
                    return View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "用户 \"" + m + "\" 重复填写！", Url = url });
                }
                templateStudents.Add(s);
                if (!vis[s.Grade])
                {
                    switch (s.Grade)
                    {
                        case 1: group += 1000; break;
                        case 2: group += 200; break;
                        case 3: group += 30; break;
                        case 4: group += 4; break;
                    }
                    vis[s.Grade] = true;
                }
                count++;
            }
            t.CID = tmp.number;
            t.Group = group;
            t.Number = count;

            if (t.Number > c.TeamLimit)
            {
                return View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "参赛人数超过限制！", Url = url });
            }

            //保存队伍
            msgBal.SaveSTeam(t);
            t = msgBal.GetTeamIDByTeam(t);

            //更新队员信息
            foreach (string m in tmp.member)
            {
                if (m == "" || m == null) continue;
                student s = msgBal.GetStudentByID(m);
                student s1 = s; s1.CertainTeam += t.ID + "&";
                msgBal.RefreshStudent(s1);
            }

            return RedirectToAction("UserDetails", "Home", new { ID = User.Identity.Name });
        }


        /// <summary>
        /// 查看队伍
        /// </summary>
        /// <param name="ID">比赛的ID</param>
        /// <returns></returns>
        public ActionResult ViewTeams(int ID)
        {
            totalmsgdbEntities msgEts = new totalmsgdbEntities();
            List<team> t = msgEts.team.ToList();
            List<team> t1 = new List<team>();
            foreach (team Team in t)
            {
                if (Team.CID == ID)
                {
                    t1.Add(Team);
                }
            }
            return View("ViewTeams", t1);
        }

        /// <summary>
        /// 删除一支队伍
        /// </summary>
        /// <param name="ID">队伍的ID</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult DeleteTeam(int? ID)
        {
            if (ID.HasValue)
            {
                MsgBusinessLayer msgBal = new MsgBusinessLayer();
                student s = msgBal.GetStudentByID(User.Identity.Name);
                msgBal.DeleteTeam(ID.Value, s);
            }
            return RedirectToAction("UserDetails", "Home", new { ID = User.Identity.Name });
        }

        /// <summary>
        /// 删除一场比赛
        /// </summary>
        /// <param name="ID">比赛的ID</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult Delete(int? ID)
        {
            if (ID.HasValue)
            {
                MsgBusinessLayer msgBal = new MsgBusinessLayer();
                student s = msgBal.GetStudentByID(User.Identity.Name);
                if (s.HasPermission == 0)
                {
                    return RedirectToAction("Index", "Home");
                }
                competition c = msgBal.GetCompetitionByID(ID.Value);
                totalmsgdbEntities msgEts = new totalmsgdbEntities();
                foreach (team t in msgEts.team.ToList())
                {
                    if (t.CID == ID)
                    {
                        msgBal.DeleteTeam(t.ID, new student() { HasPermission = 100 });
                    }
                }
                msgBal.DeleteCompetition(ID.Value);
            }
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// 编辑一场比赛
        /// </summary>
        /// <param name="ID">比赛的ID</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult Edit(int? ID)
        {
            if (!ID.HasValue) return RedirectToAction("Index", "Home");
            MsgBusinessLayer msgBal = new MsgBusinessLayer();

            //判断是否有权限
            student s = msgBal.GetStudentByID(User.Identity.Name);
            if (s.HasPermission == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            competition c = msgBal.GetCompetitionByID(ID.Value);
            return View(c);
        }

        /// <summary>
        /// 提交对比赛的修改
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Edit(TemplateCompetition c)
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            if (msgBal.RefreshCompetition(c)) return RedirectToAction("Index", "Home");
            return View(c);
        }

        /// <summary>
        /// 新建比赛
        /// </summary>
        /// <returns></returns>
        [Authorize]
        public ActionResult Add()
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            student s = msgBal.GetStudentByID(User.Identity.Name);
            if (s.HasPermission == 0)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// <summary>
        /// 提交新建的比赛的信息
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        [Authorize]
        [ValidateInput(false)]
        [HttpPost]
        public ActionResult Add(TemplateCompetition c)
        {
            MsgBusinessLayer bal = new MsgBusinessLayer();
            competition c1 = new competition();
            c1.CompetitionName = c.CompetitionName;
            c1.Details = c.Details;
            c1.StartTime = c.StartTime;
            c1.EndTime = c.EndTime;
            c1.TeamLimit = c.TeamLimit;
            c1.DeleteTime = c.DeleteTime;
            c1.Groups = 0;
            foreach (int group in c.grade)
            {
                switch (group)
                {
                    case 1: c1.Groups += 1000; break;
                    case 2: c1.Groups += 200; break;
                    case 3: c1.Groups += 30; break;
                    case 4: c1.Groups += 4; break;
                }
            }
            bal.SaveCompetition(c1);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// 导出到Excel
        /// </summary>
        /// <param name="ID">比赛ID</param>
        /// <returns></returns>
        public FileResult Excel(int ID)
        {
            //判断是否有权限
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            if (msgBal.GetStudentByID(User.Identity.Name).HasPermission == 0)
            {
                return null;
            }

            //获取list数据
            totalmsgdbEntities msgEts = new totalmsgdbEntities();
            List<team> t = msgEts.team.ToList();
            List<team> t1 = new List<team>();
            foreach (team Team in t)
            {
                if (Team.CID == ID)
                {
                    t1.Add(Team);
                }
            }

            //创建Excel文件的对象
            HSSFWorkbook book = new HSSFWorkbook();
            //添加一个sheet
            ISheet sheet1 = book.CreateSheet("Sheet1");
            int count = 1;

            //表格设计
            ICellStyle style = book.CreateCellStyle();
            style.Alignment = HorizontalAlignment.Center;
            style.VerticalAlignment = VerticalAlignment.Center;

            for (int col = 0; col < 5; col++)
            {
                sheet1.SetDefaultColumnStyle(col, style);
            }

            ICellStyle cellStyle = book.CreateCellStyle();
            cellStyle.Alignment = HorizontalAlignment.Center;
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            cellStyle.BorderBottom = BorderStyle.Thin;
            cellStyle.BorderLeft = BorderStyle.Thin;
            cellStyle.BorderRight = BorderStyle.Thin;
            cellStyle.BorderTop = BorderStyle.Thin;

            ICellStyle headerStyle = book.CreateCellStyle();
            headerStyle.Alignment = HorizontalAlignment.Center;
            headerStyle.VerticalAlignment = VerticalAlignment.Center;
            headerStyle.BorderBottom = BorderStyle.Thin;
            headerStyle.BorderLeft = BorderStyle.Thin;
            headerStyle.BorderRight = BorderStyle.Thin;
            headerStyle.BorderTop = BorderStyle.Thin;
            IFont font = book.CreateFont();
            font.Boldweight = short.MaxValue;
            headerStyle.SetFont(font);

            //给sheet1添加第一行的头部标题
            string title = msgBal.GetCompetitionByID(ID).CompetitionName + "参赛队伍信息";
            IRow row1 = sheet1.CreateRow(0);
            sheet1.AddMergedRegion(new CellRangeAddress(0, 0, 0, 4));
            row1 = sheet1.GetRow(0);
            row1.Height = 30 * 20;

            ICell cell = row1.CreateCell(0);
            cell.CellStyle = headerStyle;
            cell.SetCellValue(title);
            cell = row1.CreateCell(4);
            cell.CellStyle = headerStyle;

            //
            row1 = sheet1.CreateRow(1);
            row1.Height = 30 * 20;

            cell = row1.CreateCell(0);
            cell.CellStyle = headerStyle;
            cell.SetCellValue("队伍编号");
            sheet1.SetColumnWidth(0, 10 * 256);

            cell = row1.CreateCell(1);
            cell.CellStyle = headerStyle;
            cell.SetCellValue("队伍成员");
            sheet1.SetColumnWidth(1, 20 * 256);

            cell = row1.CreateCell(2);
            cell.CellStyle = headerStyle;
            cell.SetCellValue("学号");
            sheet1.SetColumnWidth(2, 20 * 256);

            cell = row1.CreateCell(3);
            cell.CellStyle = headerStyle;
            cell.SetCellValue("电话");
            sheet1.SetColumnWidth(3, 25 * 256);

            cell = row1.CreateCell(4);
            cell.CellStyle = headerStyle;
            cell.SetCellValue("邮箱");
            sheet1.SetColumnWidth(4, 30 * 256);

            //将数据逐步写入sheet1各个行
            int i = 1, j = 0;
            foreach (team Team in t1)
            {
                i++; j = 0;
                IRow rowTemplate;
                List<string> members = msgBal.GetMessage(Team.Member);
                foreach (string m in members)
                {
                    student s = msgBal.GetStudentByID(m);
                    rowTemplate = sheet1.CreateRow(i + j);
                    sheet1.SetColumnWidth(i + j, 30 * 256);
                    rowTemplate.Height = 30 * 20; j++;

                    cell = rowTemplate.CreateCell(1);
                    cell.CellStyle = cellStyle;
                    cell.SetCellValue(s.RealName);

                    cell = rowTemplate.CreateCell(2);
                    cell.CellStyle = cellStyle;
                    cell.SetCellValue(s.StudentID);

                    cell = rowTemplate.CreateCell(3);
                    cell.CellStyle = cellStyle;
                    cell.SetCellValue(s.Phonenumber);

                    cell = rowTemplate.CreateCell(4);
                    cell.CellStyle = cellStyle;
                    cell.SetCellValue(s.Email);
                }
                sheet1.AddMergedRegion(new CellRangeAddress(i, i + j - 1, 0, 0));

                rowTemplate = sheet1.GetRow(i);
                cell = rowTemplate.CreateCell(0);
                cell.CellStyle = cellStyle;
                cell.SetCellValue(count); count++;

                if (i + j - 1 != i)
                {
                    rowTemplate = sheet1.GetRow(i + j - 1);
                    cell = rowTemplate.CreateCell(0);
                    cell.CellStyle = cellStyle;
                }

                i = i + j - 1;
            }

            // 写入到客户端 
            MemoryStream ms = new MemoryStream();
            book.Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            competition c = msgBal.GetCompetitionByID(ID);
            return File(ms, "application/vnd.ms-excel", title + ".xls");
        }

        /// <summary>
        /// 交换用户权限
        /// </summary>
        /// <param name="ID">被交换的用户</param>
        /// <returns></returns>
        /// User.Identity.Name为交换的用户
        [Authorize]
        [HttpPost]
        public ActionResult GivePermission(string ID)
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            
            student s1 = msgBal.GetStudentByID(User.Identity.Name);
            student s2 = msgBal.GetStudentByID(ID);
            sbyte? t = s1.HasPermission;s1.HasPermission = s2.HasPermission;s2.HasPermission = t;

            msgBal.RefreshStudent(s1);
            msgBal.RefreshStudent(s2);
            return RedirectToAction("Index","Home");
        }

        /// <summary>
        /// 修改用户密码界面
        /// </summary>
        /// <param name="ID">用户的学号</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult ModifyPwd(string ID)
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            return View(msgBal.GetStudentByID(ID));
        }

        /// <summary>
        /// 修改用户密码
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public ActionResult ModifyPwd(student s)
        {
            MsgBusinessLayer msgBal = new MsgBusinessLayer();
            return msgBal.ModifyStudentPassword(s) ? View("~/Views/Home/UserSettings.cshtml", msgBal.GetStudentByID(s.StudentID)) :
                 View("~/Views/Shared/Error.cshtml", new ErrorMessage { ErrorMsg = "输入的原密码错误！", Url = "/Manage/ModifyPwd?ID="+s.StudentID });
        }
    }


    public class TemplateTeam
    {
        /// <summary>
        /// 比赛ID
        /// </summary>
        public int number { get; set; }
        /// <summary>
        /// 成员学号
        /// </summary>
        public string[] member { get; set; }
    }

    public class TemplateCompetition
    {
        public int CompetitionID { get; set; }
        public string CompetitionName { get; set; }
        public string Details { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime DeleteTime { get; set; }
        public int TeamLimit { get; set; }
        /// <summary>
        /// 复选框中参赛组别
        /// </summary>
        public int[] grade { get; set; }
    }
}