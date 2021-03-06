﻿//© Copyright 2013 Hewlett-Packard Development Company, L.P.
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software
//and associated documentation files (the "Software"), to deal in the Software without restriction,
//including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
//and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
//subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or
//substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
//PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
//LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
//TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
//OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Xml;
using Analysis.Api;
//using Analysis.ApiLib;
using Analysis.ApiLib.Sla;
using LRAnalysisLauncher.Properties;
using HpToolsLauncher;
using Analysis.ApiSL;
using Analysis.Api.Dictionaries;
using System.Diagnostics;

namespace LRAnalysisLauncher
{
    class Program
    {

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        static void log()
        {
            log("");
        }
        static void log(string msg)
        {
            Console.WriteLine(msg);
          //  writer.WriteLine(msg);
        }
//        static StreamWriter writer = new StreamWriter(new FileStream("c:\\AnalysisLauncherOutput.txt", FileMode.OpenOrCreate, FileAccess.Write));
        //args: lrr location, lra location, html report location
        [STAThread]
        static int Main(string[] args)
        {
            
            log("starting analysis launcher");
            int iPassed = (int)Launcher.ExitCodeEnum.Passed;//variable to keep track of whether all of the SLAs passed
            try
            {
                if (args.Length != 3)
                {
                    ShowHelp();
                    return (int)Launcher.ExitCodeEnum.Aborted;
                }

                string lrrlocation = args[0];
                string lralocation = args[1];
                string htmlLocation = args[2];

                log("creating analysis COM object");
                LrAnalysis analysis = new LrAnalysis();

                Session session = analysis.Session;
                log("creating analysis session");
                if (session.Create(lralocation, lrrlocation))
                {
                    log("analysis session created");
                    log("creating HTML reports");
                    HtmlReportMaker reportMaker = session.CreateHtmlReportMaker();
                    reportMaker.CreateDefaultHtmlReport(Path.Combine(Path.GetDirectoryName(htmlLocation), "IE", Path.GetFileName(htmlLocation)), ApiBrowserType.IE);
                    reportMaker.CreateDefaultHtmlReport(Path.Combine(Path.GetDirectoryName(htmlLocation), "Netscape", Path.GetFileName(htmlLocation)), ApiBrowserType.Netscape);
                    log("HTML reports created");

                    XmlDocument xmlDoc = new XmlDocument();

                    log("loading errors, if any");
                    session.ErrorMessages.LoadValuesIfNeeded();
                    if (session.ErrorMessages.Count != 0)
                    {
                        log("error count: " + session.ErrorMessages.Count);
                        if (session.ErrorMessages.Count > 1000)
                        {
                            log("more then 1000 error during scenario run, analyzing only the first 1000.");
                        }
                        log(Resources.ErrorsReportTitle);
                        XmlElement errorRoot = xmlDoc.CreateElement("Errors");
                        xmlDoc.AppendChild(errorRoot);
                        int limit = 1000;
                        ErrorMessage[] errors = session.ErrorMessages.ToArray();
                        //foreach (ErrorMessage err in session.ErrorMessages)
                        for (int i = 0; i < limit && i < errors.Length; i++)
                        {
                            ErrorMessage err = errors[i];
                            XmlElement elem = xmlDoc.CreateElement("Error");
                            elem.SetAttribute("ID", err.ID.ToString());
                            elem.AppendChild(xmlDoc.CreateTextNode(err.Name));
                            log("ID: " + err.ID + " Name: " + err.Name);
                            errorRoot.AppendChild(elem);
                        }
                        xmlDoc.Save(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(lrrlocation)), "Errors.xml"));

                        xmlDoc.RemoveAll();
                        log ("");
                    }
                    log("closing session");
                    session.Close();
                    log(Resources.SLAReportTitle);
                    log("calculating SLA");
                    SlaResult slaResult = Session.CalculateSla(lralocation, true);
                    log("SLA calculation done");
                    XmlElement root = xmlDoc.CreateElement("SLA");
                    xmlDoc.AppendChild(root);

                    int iCounter = 0; // set counter
                    log("WholeRunRules : " + slaResult.WholeRunRules.Count);
                    foreach (SlaWholeRunRuleResult a in slaResult.WholeRunRules)
                    {
                        log(Resources.DoubleLineSeperator);
                        XmlElement elem;
                        if (a.Measurement.Equals(SlaMeasurement.PercentileTRT))
                        {
                            SlaPercentileRuleResult b = slaResult.TransactionRules.PercentileRules[iCounter];
                            elem = xmlDoc.CreateElement(b.RuleName.Replace(" ", "_"));    //no white space in the element name
                            log("Transaction Name : " + b.TransactionName);
                            elem.SetAttribute("TransactionName", b.TransactionName.ToString());
                            log("Percentile : " + b.Percentage);
                            elem.SetAttribute("Percentile", b.Percentage.ToString());
                            elem.SetAttribute("FullName", b.RuleUiName);
                            log("Full Name : " + b.RuleUiName);
                            log("Measurement : " + b.Measurement);
                            elem.SetAttribute("Measurement", b.Measurement.ToString());
                            log("Goal Value : " + b.GoalValue);
                            elem.SetAttribute("GoalValue", b.GoalValue.ToString());
                            log("Actual value : " + b.ActualValue);
                            elem.SetAttribute("ActualValue", b.ActualValue.ToString());
                            log("status : " + b.Status);
                            elem.AppendChild(xmlDoc.CreateTextNode(b.Status.ToString()));

                            if (b.Status.Equals(SlaRuleStatus.Failed)) // 0 = failed
                            {
                                iPassed = (int)Launcher.ExitCodeEnum.Failed;
                            }
                            iCounter++;
                        }
                        else
                        {
                            elem = xmlDoc.CreateElement(a.RuleName.Replace(" ", "_"));    //no white space in the element name
                            elem.SetAttribute("FullName", a.RuleUiName);
                            log("Full Name : " + a.RuleUiName);
                            log("Measurement : " + a.Measurement);
                            elem.SetAttribute("Measurement", a.Measurement.ToString());
                            log("Goal Value : " + a.GoalValue);
                            elem.SetAttribute("GoalValue", a.GoalValue.ToString());
                            log("Actual value : " + a.ActualValue);
                            elem.SetAttribute("ActualValue", a.ActualValue.ToString());
                            log("status : " + a.Status);
                            elem.AppendChild(xmlDoc.CreateTextNode(a.Status.ToString()));

                            if (a.Status.Equals(SlaRuleStatus.Failed)) // 0 = failed
                            {
                                iPassed = (int)Launcher.ExitCodeEnum.Failed;
                            }
                        }
                        root.AppendChild(elem);
                        log(Resources.DoubleLineSeperator);
                    }

                    iCounter = 0; // reset counter
                    log("TimeRangeRules : " + slaResult.TimeRangeRules.Count);
                    foreach (SlaTimeRangeRuleResult a in slaResult.TimeRangeRules)
                    {
                        
                        log(Resources.DoubleLineSeperator);
                        XmlElement rule;
                        if (a.Measurement.Equals(SlaMeasurement.AverageTRT))
                         {
                            SlaTransactionTimeRangeRuleResult b = slaResult.TransactionRules.TimeRangeRules[iCounter];
                            rule = xmlDoc.CreateElement(b.RuleName.Replace(" ", "_"));      //no white space in the element name
                            log("Transaction Name: " + b.TransactionName);
                            rule.SetAttribute("TransactionName", b.TransactionName);
                            log("Full Name : " + b.RuleUiName);
                            rule.SetAttribute("FullName", b.RuleUiName);
                            log("Measurement : " + b.Measurement);
                            rule.SetAttribute("Measurement", b.Measurement.ToString());
                            log("SLA Load Threshold Value : " + b.CriteriaMeasurement);
                            rule.SetAttribute("SLALoadThresholdValue", b.CriteriaMeasurement.ToString());
                            log("LoadThresholds : " + b.LoadThresholds.Count);
                            foreach (SlaLoadThreshold slat in b.LoadThresholds)
                            {
                                XmlElement loadThr = xmlDoc.CreateElement("SlaLoadThreshold");
                                loadThr.SetAttribute("StartLoadValue", slat.StartLoadValue.ToString());
                                loadThr.SetAttribute("EndLoadValue", slat.EndLoadValue.ToString());
                                loadThr.SetAttribute("ThresholdValue", slat.ThresholdValue.ToString());
                                rule.AppendChild(loadThr);

                            }
                            XmlElement timeRanges = xmlDoc.CreateElement("TimeRanges");
                            log("TimeRanges : " + b.TimeRanges.Count);
                            foreach (SlaTimeRangeInfo slatri in b.TimeRanges)
                            {
                                XmlElement subsubelem = xmlDoc.CreateElement("TimeRangeInfo");
                                subsubelem.SetAttribute("StartTime", slatri.StartTime.ToString());
                                subsubelem.SetAttribute("EndTime", slatri.EndTime.ToString());
                                subsubelem.SetAttribute("GoalValue", slatri.GoalValue.ToString());
                                subsubelem.SetAttribute("ActualValue", slatri.ActualValue.ToString());
                                subsubelem.SetAttribute("LoadValue", slatri.LoadValue.ToString());
                                subsubelem.InnerText = slatri.Status.ToString();
                                timeRanges.AppendChild(subsubelem);
                            }
                            rule.AppendChild(timeRanges);
                           log("status : " + b.Status);
                            rule.AppendChild(xmlDoc.CreateTextNode(b.Status.ToString()));
                            if (b.Status.Equals(SlaRuleStatus.Failed)) // 0 = failed
                            {
                                iPassed = (int)Launcher.ExitCodeEnum.Failed;
                            }
                            iCounter++;
                         }
                        else
                        {
                            rule = xmlDoc.CreateElement(a.RuleName.Replace(" ", "_"));  //no white space in the element name
                            log("Full Name : " + a.RuleUiName);
                            rule.SetAttribute("FullName", a.RuleUiName);
                            log("Measurement : " + a.Measurement);
                            rule.SetAttribute("Measurement", a.Measurement.ToString());
                            log("SLA Load Threshold Value : " + a.CriteriaMeasurement);
                            rule.SetAttribute("SLALoadThresholdValue", a.CriteriaMeasurement.ToString());
                            log("LoadThresholds : " + a.LoadThresholds.Count);
                            foreach (SlaLoadThreshold slat in a.LoadThresholds)
                            {
                                XmlElement loadThr = xmlDoc.CreateElement("SlaLoadThreshold");
                                loadThr.SetAttribute("StartLoadValue", slat.StartLoadValue.ToString());
                                loadThr.SetAttribute("EndLoadValue", slat.EndLoadValue.ToString());
                                loadThr.SetAttribute("ThresholdValue", slat.ThresholdValue.ToString());
                                rule.AppendChild(loadThr);

                            }
                            XmlElement timeRanges = xmlDoc.CreateElement("TimeRanges");
                            log("TimeRanges : " + a.TimeRanges.Count);
                            foreach (SlaTimeRangeInfo slatri in a.TimeRanges)
                            {
                                XmlElement subsubelem = xmlDoc.CreateElement("TimeRangeInfo");
                                subsubelem.SetAttribute("StartTime", slatri.StartTime.ToString());
                                subsubelem.SetAttribute("EndTime", slatri.EndTime.ToString());
                                subsubelem.SetAttribute("GoalValue", slatri.GoalValue.ToString());
                                subsubelem.SetAttribute("ActualValue", slatri.ActualValue.ToString());
                                subsubelem.SetAttribute("LoadValue", slatri.LoadValue.ToString());
                                subsubelem.InnerText = slatri.Status.ToString();
                                timeRanges.AppendChild(subsubelem);
                            }
                            rule.AppendChild(timeRanges);
                            log("status : " + a.Status);
                            rule.AppendChild(xmlDoc.CreateTextNode(a.Status.ToString()));
                            if (a.Status.Equals(SlaRuleStatus.Failed))
                            {
                                iPassed = (int)Launcher.ExitCodeEnum.Failed;
                            }
                        
                        }
                        root.AppendChild(rule);

                        log(Resources.DoubleLineSeperator);
                    }

                    //write XML to location:
                    log("saving SLA.xml to " + Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(lrrlocation)), "SLA.xml"));
                    xmlDoc.Save(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(lrrlocation)), "SLA.xml"));

                }
                else
                {

                    log(Resources.CannotCreateSession);
                    return (int)Launcher.ExitCodeEnum.Aborted;
                }
                log("closing analysis session");
                session.Close();
            }
            catch (TypeInitializationException ex)
            {
                if (ex.InnerException is UnauthorizedAccessException)
                    log("UnAuthorizedAccessException: Please check the account privilege of current user, LoadRunner tests should be run by administrators.");
                else
                {
                    log(ex.Message);
                    log(ex.StackTrace);
                }
                return (int)Launcher.ExitCodeEnum.Aborted;
            }
            catch (Exception e)
            {
                log(e.Message);
                log(e.StackTrace);
                return (int)Launcher.ExitCodeEnum.Aborted;
            }

            // return SLA status code, if any SLA fails return a fail here.
           // writer.Flush();
           // writer.Close();
            return iPassed;

        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            System.Reflection.AssemblyName name = new System.Reflection.AssemblyName(args.Name);
            if (name.Name.ToLowerInvariant().EndsWith(".resources")) return null;
            string installPath = HpToolsLauncher.Helper.getLRInstallPath();
            if (installPath == null)
            {
                log(Resources.CannotLocateInstallDir);
                Environment.Exit((int)Launcher.ExitCodeEnum.Aborted);
            }
            //log(Path.Combine(installPath, "bin", name.Name + ".dll"));
            return System.Reflection.Assembly.LoadFrom(Path.Combine(installPath, "bin", name.Name + ".dll"));
        }

        private static void ShowHelp()
        {
            log("HP LoadRunner Analysis Command Line Executer");
            log();
            Console.Write("Usage: LRAnalysisLauncher.exe");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[.lrr file location] [.lra output location] [html report output folder]");
            Console.ResetColor();
            Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
        }
    }
}
