using System;
using System.Collections.Generic;
using System.Text;
using WinSCP;
using System.IO;
using CincApplicationLog;

namespace CincFileDelivery
{
    class CincSFTP : IDisposable
    {
        private Session _session = null;
        private CApplicationLog _log = null;

        public CincSFTP(ref CApplicationLog log)
        {
            try
            {
                string host = System.Configuration.ConfigurationManager.AppSettings["host"];
                int port = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["port"]);
                string uid = System.Configuration.ConfigurationManager.AppSettings["uid"];
                string pwd = System.Configuration.ConfigurationManager.AppSettings["pwd"];
                string privateKeyFile = System.Configuration.ConfigurationManager.AppSettings["privatekey"];
                string hostFingerprint = System.Configuration.ConfigurationManager.AppSettings["hostfingerprint"];
                _log = log;

                SessionOptions sessionOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = host,
                    PortNumber = port,
                    UserName = uid,
                    Password = pwd,
                    SshHostKeyFingerprint = hostFingerprint,
                    SshPrivateKeyPath = privateKeyFile
                };
                _session = new Session();
                _session.Open(sessionOptions);
            }
            catch (Exception ex)
            {
                _log.LogErr("CincSFTP", "CincSFTP", ex.Message);
                if (null != _session)
                {
                    _session.Dispose();
                    _session = null;
                }
            }
        }

        public CincSFTP(ref CApplicationLog log, string username, string password, string hostname, int port, string host, string hostFingerPrint)
        {
            try
            {
                _log = log;

                SessionOptions sessionOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = host,
                    PortNumber = port,
                    UserName = username,
                    Password = password,
                    SshHostKeyFingerprint = hostFingerPrint,
                    //SshPrivateKeyPath = privateKeyFile
                };
                _session = new Session();
                _session.Open(sessionOptions);
            }
            catch (Exception ex)
            {
                _log.LogErr("CincSFTP", "CincSFTP", ex.Message);
                if (null != _session)
                {
                    _session.Dispose();
                    _session = null;
                }
            }
        }

        public bool PutFiles(string localPath, string remotePath)
        {
            try
            {
                if (null != _session)
                {
                    if (remotePath[remotePath.Length - 1] != '/')
                        remotePath += "";
                    TransferOptions transferOptions = new TransferOptions();
                    transferOptions.TransferMode = TransferMode.Automatic;
                    TransferOperationResult transferResult;
                    transferResult = _session.PutFiles(localPath, remotePath, false, transferOptions);
                    transferResult.Check();
                    return true;
                }
                else
                    _log.LogErr("CincSFTP", "PutFiles", "SessionClosed");
            }
            catch (Exception ex)
            {
                _log.LogErr("CincSFTP", "PutFiles", ex.Message);
            }
            return false;
        }

        public bool GetFiles(string localPath, string remotePath, string filePattern, bool deleteFromRemote)
        {
            try
            {
                if (null != _session)
                {
                    if (localPath[localPath.Length - 1] != '\\')
                        localPath += "\\";
                    if (remotePath[remotePath.Length - 1] != '/')
                        remotePath += "/";
                    TransferOptions transferOptions = new TransferOptions();
                    transferOptions.TransferMode = TransferMode.Automatic;
                    TransferOperationResult transferResult;
                    if (filePattern == "") //If no pattern is passed, get all the files in the remote folder
                        transferResult = _session.GetFiles(remotePath, localPath, deleteFromRemote, transferOptions);
                    else //Get the files from the remote folder that match the specified pattern
                        transferResult = _session.GetFiles(remotePath + filePattern, localPath, deleteFromRemote, transferOptions);
                    transferResult.Check();
                    return true;
                }
                else
                    _log.LogErr("CincSFTP", "GetFiles", "SessionClosed");
            }
            catch (Exception ex)
            {
                _log.LogErr("CincSFTP", "GetFiles", ex.Message);
            }
            return false;
        }

        public void Dispose()
        {
            _session.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
