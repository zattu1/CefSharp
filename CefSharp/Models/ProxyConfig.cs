using System;

namespace CefSharp.fastBOT.Models
{
    /// <summary>
    /// �v���L�V�ݒ�����i�[����N���X
    /// </summary>
    public class ProxyConfig
    {
        /// <summary>
        /// �v���L�V�z�X�g
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// �v���L�V�|�[�g
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// �v���L�V�X�L�[���ihttp, https, socks5�Ȃǁj
        /// </summary>
        public string Scheme { get; set; } = "http";

        /// <summary>
        /// �F�ؗp���[�U�[��
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// �F�ؗp�p�X���[�h
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// �v���L�V���L�����ǂ���
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        public ProxyConfig()
        {
        }

        /// <summary>
        /// �R���X�g���N�^�i��{���j
        /// </summary>
        /// <param name="host">�z�X�g</param>
        /// <param name="port">�|�[�g</param>
        /// <param name="scheme">�X�L�[��</param>
        public ProxyConfig(string host, int port, string scheme = "http")
        {
            Host = host;
            Port = port;
            Scheme = scheme;
        }

        /// <summary>
        /// �R���X�g���N�^�i�F�ؕt���j
        /// </summary>
        /// <param name="host">�z�X�g</param>
        /// <param name="port">�|�[�g</param>
        /// <param name="username">���[�U�[��</param>
        /// <param name="password">�p�X���[�h</param>
        /// <param name="scheme">�X�L�[��</param>
        public ProxyConfig(string host, int port, string username, string password, string scheme = "http")
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            Scheme = scheme;
        }

        /// <summary>
        /// �F�؂��K�v���ǂ���
        /// </summary>
        public bool RequiresAuthentication => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

        /// <summary>
        /// �v���L�VURL���擾
        /// </summary>
        /// <returns>�v���L�VURL</returns>
        public string GetProxyUrl()
        {
            if (RequiresAuthentication)
            {
                return $"{Scheme}://{Username}:{Password}@{Host}:{Port}";
            }
            else
            {
                return $"{Scheme}://{Host}:{Port}";
            }
        }

        /// <summary>
        /// ������\�����擾
        /// </summary>
        /// <returns>�v���L�V���̕�����</returns>
        public override string ToString()
        {
            var auth = RequiresAuthentication ? " (�F�؂���)" : "";
            return $"{Scheme}://{Host}:{Port}{auth}";
        }

        /// <summary>
        /// �v���L�V��������p�[�X�ihost:port:user:pass�`���j
        /// </summary>
        /// <param name="proxyText">�v���L�V������</param>
        /// <returns>ProxyConfig�I�u�W�F�N�g</returns>
        public static ProxyConfig Parse(string proxyText)
        {
            if (string.IsNullOrWhiteSpace(proxyText))
                return null;

            try
            {
                var parts = proxyText.Trim().Split(':');
                if (parts.Length < 2)
                    return null;

                var config = new ProxyConfig
                {
                    Host = parts[0].Trim(),
                    Port = int.Parse(parts[1].Trim())
                };

                // ���[�U�[���ƃp�X���[�h������ꍇ
                if (parts.Length >= 4)
                {
                    config.Username = parts[2].Trim();
                    config.Password = parts[3].Trim();
                }

                // �X�L�[���̔���i�|�[�g�ԍ����琄���j
                if (config.Port == 1080 || config.Port == 1081)
                {
                    config.Scheme = "socks5";
                }
                else
                {
                    config.Scheme = "http";
                }

                return config;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"�v���L�V������̃p�[�X�G���[: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// �ݒ肪�L�����ǂ���������
        /// </summary>
        /// <returns>�L���ȏꍇtrue</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Host) &&
                   Port > 0 && Port <= 65535 &&
                   !string.IsNullOrWhiteSpace(Scheme);
        }
    }
}