﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HslCommunication;
using System.Threading;
using Newtonsoft.Json.Linq;
using CommonLibrary;
using HslCommunication.BasicFramework;

namespace ClientsLibrary
{

    /*********************************************************************************
     * 
     *    统一的账户登录模型
     * 
     * 
     *********************************************************************************/


    /// <summary>
    /// 用户客户端使用的统一登录的逻辑
    /// </summary>
    public class AccountLogin
    {
        /// <summary>
        /// 系统统一的登录模型
        /// </summary>
        /// <param name="message_show">信息提示方法</param>
        /// <param name="start_update">启动更新方法</param>
        /// <param name="thread_finish">线程结束后的复原方法</param>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="remember">是否记住登录密码</param>
        /// <param name="clientType">客户端登录类型</param>
        /// <returns></returns>
        public static bool AccountLoginServer(
            Action<string> message_show, 
            Action start_update, 
            Action thread_finish,
            string userName,
            string password,
            bool remember,
            string clientType
            )
        {
            message_show.Invoke("正在维护检查...");

            Thread.Sleep(200);
            
            // 请求指令头数据，该数据需要更具实际情况更改
            OperateResultString result = UserClient.Net_simplify_client.ReadFromServer(CommonLibrary.CommonHeadCode.SimplifyHeadCode.维护检查);
            if (result.IsSuccess)
            {
                byte[] temp = Encoding.Unicode.GetBytes(result.Content);
                // 例如返回结果为1说明允许登录，0则说明服务器处于维护中，并将信息显示
                if (result.Content != "1")
                {
                    message_show.Invoke(result.Content.Substring(1));
                    thread_finish.Invoke();
                    return false;
                }
            }
            else
            {
                // 访问失败
                message_show.Invoke(result.Message);
                thread_finish.Invoke();
                return false;
            }



            // 检查账户
            message_show.Invoke("正在检查账户...");

            // 延时
            Thread.Sleep(200);

            //===================================================================================
            //   根据实际情况校验，选择数据库校验或是将用户名密码发至服务器校验
            //   以下展示了服务器校验的方法，如您需要数据库校验，请删除下面并改成SQL访问验证的方式

            // 包装数据
            JObject json = new JObject
            {
                { UserAccount.UserNameText, new JValue(userName) },
                { UserAccount.PasswordText, new JValue(password) },
                { UserAccount.LoginWayText, new JValue(clientType) },
                { UserAccount.DeviceUniqueID, new JValue(UserClient.JsonSettings.SystemInfo) }
            };
            result = UserClient.Net_simplify_client.ReadFromServer(CommonHeadCode.SimplifyHeadCode.账户检查, json.ToString());
            if (result.IsSuccess)
            {
                // 服务器应该返回账户的信息
                UserAccount account = JObject.Parse(result.Content).ToObject<UserAccount>();
                if (!account.LoginEnable)
                {
                    // 不允许登录
                    message_show.Invoke(account.ForbidMessage);
                    thread_finish.Invoke();
                    return false;
                }
                UserClient.UserAccount = account;
            }
            else
            {
                // 访问失败
                message_show.Invoke(result.Message);
                thread_finish.Invoke();
                return false;
            }

            // 登录成功，进行保存用户名称和密码
            UserClient.JsonSettings.LoginName = userName;
            UserClient.JsonSettings.Password = remember ? password : "";
            UserClient.JsonSettings.LoginTime = DateTime.Now;
            UserClient.JsonSettings.SaveToFile();


            // 版本验证
            message_show.Invoke("正在验证版本...");

            // 延时
            Thread.Sleep(200);

            result = UserClient.Net_simplify_client.ReadFromServer(CommonLibrary.CommonHeadCode.SimplifyHeadCode.更新检查);
            if (result.IsSuccess)
            {
                // 服务器应该返回服务器的版本号
                SystemVersion sv = new SystemVersion(result.Content);
                // 系统账户跳过低版本检测，该做法存在一定的风险，需要开发者仔细确认安全隐患
                if (UserClient.UserAccount.UserName != "admin")
                {
                    if (UserClient.CurrentVersion != sv)
                    {
                        // 保存新版本信息
                        UserClient.JsonSettings.IsNewVersionRunning = true;
                        UserClient.JsonSettings.SaveToFile();
                        // 和当前系统版本号不一致，启动更新
                        start_update.Invoke();
                        return false;
                    }
                }
                else
                {
                    if (UserClient.CurrentVersion < sv)
                    {
                        // 保存新版本信息
                        UserClient.JsonSettings.IsNewVersionRunning = true;
                        UserClient.JsonSettings.SaveToFile();
                        // 和当前系统版本号不一致，启动更新
                        start_update.Invoke();
                        return false;
                    }
                }
            }
            else
            {
                // 访问失败
                message_show.Invoke(result.Message);
                thread_finish.Invoke();
                return false;
            }


            //
            // 验证结束后，根据需要是否下载服务器的数据，或是等到进入主窗口下载也可以
            // 如果有参数决定主窗口的显示方式，那么必要在下面向服务器请求数据
            // 以下展示了初始化参数的功能

            message_show.Invoke("正在下载参数...");

            // 延时
            Thread.Sleep(200);


            result = UserClient.Net_simplify_client.ReadFromServer(CommonLibrary.CommonHeadCode.SimplifyHeadCode.参数下载);
            if (result.IsSuccess)
            {
                // 服务器返回初始化的数据，此处进行数据的提取，有可能包含了多个数据
                json = JObject.Parse(result.Content);
                // 例如公告数据
                UserClient.Announcement = SoftBasic.GetValueFromJsonObject(json, nameof(UserClient.Announcement), "");
                if (json[nameof(UserClient.SystemFactories)] != null)
                {
                    UserClient.SystemFactories = json[nameof(UserClient.SystemFactories)].ToObject<List<string>>();
                }
            }
            else
            {
                // 访问失败
                message_show.Invoke(result.Message);
                thread_finish.Invoke();
                return false;
            }

            return true;
        }
        
    }
}