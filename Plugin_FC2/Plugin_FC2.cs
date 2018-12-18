//�v���O�C���̃t�@�C�����́A�uPlugin_*.dll�v�Ƃ����`���ɂ��ĉ������B
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Net;
using System.ComponentModel;
using System.Windows.Forms;
using Newtonsoft.Json;
using FNF.Utility;
using FNF.Controls;
using FNF.XmlSerializerSetting;
using FNF.BouyomiChanApp;

namespace Plugin_FC2 {
    public class Plugin_FC2 : IPlugin {
        #region ���t�B�[���h

        private Settings_FC2           _Settings;                                                       //�ݒ�
        private SettingFormData_FC2    _SettingFormData;
        private string                 _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //�ݒ�t�@�C���̕ۑ��ꏊ
        private System.Threading.Timer _Timer;                                                          //�^�C�}
        private ToolStripButton        _Button;
        private ToolStripSeparator     _Separator;
        private bool _IconStatus;
        private bool _SettingStatus;
        private long _LastCommentIndex;
        private long _LastCommentIndexTmp;

        #endregion


        #region ��IPlugin�����o�̎���

        public string           Name            { get { return "FC2���C�u�ǂݏグ API ver"; } }

        public string           Version         { get { return "2018/12/16��"; } }

        public string           Caption         { get { return "FC2���C�u�̃R�����g��ǂݏグ�܂��B"; } }

        //�v���O�C���̐ݒ��ʏ��i�ݒ��ʂ��K�v�Ȃ����null��Ԃ��j
        public ISettingFormData SettingFormData { get { return _SettingFormData; } }

        //�v���O�C���J�n������
        public void Begin() {
            //�ݒ�t�@�C���ǂݍ���
            this._Settings = new Settings_FC2(this);
            this._Settings.Load(this._SettingFile);
            this._SettingFormData = new SettingFormData_FC2(this._Settings);
            this._IconStatus = true;
            this._SettingStatus = true;
            this._LastCommentIndex = -1;
            this._LastCommentIndexTmp = -2;

            //�^�C�}�[�o�^
            this._Timer = new System.Threading.Timer(this.Timer_Event, null, 0, 1000);

            //��ʂɃ{�^���ƃZ�p���[�^��ǉ�
            this._Separator = new ToolStripSeparator();
            Pub.ToolStrip.Items.Add(this._Separator);
            this._Button = new ToolStripButton(Properties.Resources.ImgFC2Off);
            this._Button.ToolTipText = "FC2���C�u�ǂݏグ ON/OFF";
            this._Button.Click      += this.Button_Click;
            Pub.ToolStrip.Items.Add(this._Button);
        }

        //�v���O�C���I��������
        public void End() {
            //�ݒ�t�@�C���ۑ�
            this._Settings.Save(_SettingFile);

            //�^�C�}�J��
            if (this._Timer != null) {
                this._Timer.Dispose();
                this._Timer = null;
            }

            //��ʂ���{�^���ƃZ�p���[�^���폜
            if (this._Separator != null) {
                Pub.ToolStrip.Items.Remove(this._Separator);
                this._Separator.Dispose();
                this._Separator = null;
            }
            if (this._Button != null) {
                Pub.ToolStrip.Items.Remove(this._Button);
                this._Button.Dispose();
                this._Button = null;
            }
        }

        #endregion


        #region �����\�b�h�E�C�x���g����

        private void ChangeIcon(bool flg) {
            try {
                if(flg || this._IconStatus != flg) {
                    this._IconStatus = flg;
                    Bitmap image = flg ? Properties.Resources.ImgFC2 : Properties.Resources.ImgFC2Off;
                    this._Button.Image = image;
                }
            } catch(Exception e) {
                this.PrintException(e);
            }
        }

        //�^�C�}�[�C�x���g
        private void Timer_Event(object obj) {
            if (this._Settings.TimeSignal) {
                this.AddCommentTalk(this._LastCommentIndex);
            } else {
                this.ChangeIcon(false);
            }
        }

        private void Button_Click(object sender, EventArgs e) {
            if (!this._Settings.TimeSignal && !this._SettingStatus) {
                Pub.ShowSettingForm(this);
            } else { 
                this.ResetCommentIndex();
                this._Settings.TimeSignal = !this._Settings.TimeSignal;
                Pub.ClearTalkTasks();
            }
        }

        internal void ResetCommentIndex() {
            this._LastCommentIndex = -1;
            this._LastCommentIndexTmp = -2;
        }

        internal void SetSettingStatus() {
            this._SettingStatus = (this._Settings.Token != "" && this._Settings.ChannelID != "");
        }

        private void AddCommentTalk(long lastCommentIndex) {
            if(this._LastCommentIndexTmp != lastCommentIndex) {
                this._LastCommentIndexTmp = lastCommentIndex;
                try {
                    CommentData commentData = sendRequest(lastCommentIndex);
                    this._LastCommentIndexTmp = -2;
                    switch (commentData.status) {
                        case 0:
                            this._LastCommentIndex = commentData.last_comment_index;
                            if (lastCommentIndex >= 0) {
                                foreach (Comment comment in commentData.comments) {
                                    Pub.AddTalkTask(comment.comment, -1, -1, VoiceType.Default);
                                }
                            } else {
                                this.ChangeIcon(true);
                                //Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���J�n���܂�", -1, -1, VoiceType.Default);
                            }
                            break;
                        case 10:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���I�����܂�", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("API�A�N�Z�X�p�����[�^�[������Ă��܂���", -1, -1, VoiceType.Default);
                            break;
                        case 11:
                            this._Settings.TimeSignal = false;
                            this._SettingStatus = false;
                            Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���I�����܂�", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("�g�[�N��������������܂���", -1, -1, VoiceType.Default);
                            break;
                        case 12:
                            this._Settings.TimeSignal = false;
                            this._SettingStatus = false;
                            Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���I�����܂�", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("�`�����l����������܂���", -1, -1, VoiceType.Default);
                            break;
                        case 13:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���I�����܂�", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("�������Ă��Ȃ��`�����l���ł�", -1, -1, VoiceType.Default);
                            break;
                        case 98:
                        case 99:
                        default:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2���C�u�̓ǂݏグ���I�����܂�", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("�G���[���������܂���", -1, -1, VoiceType.Default);
                            break;
                    }
                } catch (Exception e) {
                    this._LastCommentIndexTmp = -2;
                    this.PrintException(e);
                }
            }
        }

        private void PrintException(Exception e) {
            Console.WriteLine("Message     :n" + e.Message);
            Console.WriteLine("Type        :n" + e.GetType().FullName);
            Console.WriteLine("StackTrace  :n" + e.StackTrace.ToString());
        }

        private CommentData sendRequest(long lastCommentIndex) {
            StringBuilder sb = new StringBuilder();
            sb.Append("https://live.fc2.com/api/getChannelComment.php");
            sb.Append("?channel_id=");
            sb.Append(this._Settings.ChannelID);
            sb.Append("&token=");
            sb.Append(this._Settings.Token);
            sb.Append("&last_comment_index=");
            sb.Append(lastCommentIndex);
            WebClient wc = new WebClient();
            Stream st = wc.OpenRead(sb.ToString());
            Encoding enc = Encoding.GetEncoding("UTF-8");
            StreamReader sr = new StreamReader(st, enc);
            string json = sr.ReadToEnd();
            sr.Close();
            st.Close();

            CommentData data = JsonConvert.DeserializeObject<CommentData>(json);
            return data;
            

            //XmlDocument doc = JsonToXML(json);
            //XmlNodeList elem = doc.GetElementsByTagName("status");

            //return elem[0].InnerText;


        }

        //private XmlDocument JsonToXML(string json) {
        //    XmlDocument doc = new XmlDocument();
        //    using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.Unicode.GetBytes(json), XmlDictionaryReaderQuotas.Max)){
        //        XElement xml = XElement.Load(reader);
        //        doc.LoadXml(xml.ToString());
        //    }

        //    return doc;
        //}

        #endregion


        #region ���N���X�E�\����

        public class CommentData {
            public int status;
            public IList<Comment> comments;
            public long last_comment_index;
        }

        public class Comment {
            public string user_name;
            public long timestamp;
            public string lang;
            public string hash;
            public int anonymous;
            public string user_id_hash;
            public int owner;
            public string comment;
            public string color;
            public string size;
            public SystemComment system_comment;
        }

        public class SystemComment {
            public string type;
            public int tip_amount;
            public int tip_total;
        }

        // �ݒ�N���X�i�ݒ��ʕ\���E�t�@�C���ۑ����ȗ����Bpublic�ȃ����o�����ۑ������BXmlSerializer�ŏ����ł���N���X�̂ݎg�p�B�j
        public class Settings_FC2 : SettingsBase {
            //�ۑ��������i�ݒ��ʂ�����Q�Ƃ����j
            public bool TimeSignal = true;

            public string ChannelID;
            public string Token;
            public string CommentRegExp;
            public string TipRegExp;
            public string GiftRegExp;

            //�쐬���v���O�C��
            internal Plugin_FC2 Plugin;

            //�R���X�g���N�^
            public Settings_FC2() {
            }

            //�R���X�g���N�^
            public Settings_FC2(Plugin_FC2 pFC2) {
                this.Plugin = pFC2;
            }

            //GUI�Ȃǂ��瓖�I�u�W�F�N�g�̓ǂݍ���(�ݒ�Z�[�u���E�ݒ��ʕ\�����ɌĂ΂��)
            public override void ReadSettings() {
                this.Plugin.SetSettingStatus();
                this.Plugin.ResetCommentIndex();
            }

            //���I�u�W�F�N�g����GUI�Ȃǂւ̔��f(�ݒ胍�[�h���E�ݒ�X�V���ɌĂ΂��)
            public override void WriteSettings() {
                this.Plugin.SetSettingStatus();
                this.Plugin.ResetCommentIndex();
            }
        }

        // �ݒ��ʕ\���p�N���X�i�ݒ��ʕ\���E�t�@�C���ۑ����ȗ����Bpublic�ȃ����o�����ۑ������BXmlSerializer�ŏ����ł���N���X�̂ݎg�p�B�j
        public class SettingFormData_FC2 : ISettingFormData {
            Settings_FC2 _Setting;

            public string       Title     { get { return _Setting.Plugin.Name; } }
            public bool         ExpandAll { get { return false; } }
            public SettingsBase Setting   { get { return _Setting; } }

            public SettingFormData_FC2(Settings_FC2 setting) {
                this._Setting = setting;
                this.PBase    = new SBase(this._Setting);
            }

            //�ݒ��ʂŕ\�������N���X(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid {
                Settings_FC2 _Setting;
                public SBase(Settings_FC2 setting) { this._Setting = setting; }
                public string GetName() { return "�ݒ�"; }

                [Category   ("��{�ݒ�")]
                [DisplayName("�ǂݏグ��L���ɂ���")]
                public bool TimeSignal { get { return this._Setting.TimeSignal; } set { this._Setting.TimeSignal = value; } }

                [Category   ("��{�ݒ�")]
                [DisplayName("�`�����l��ID")]
                [Description("�ǂݏグ�Ώۂ̃`�����l��ID\nhttps://live.fc2.com/65177747/ �Ȃ� 65177747")]
                public string ChannelID { get { return this._Setting.ChannelID; } set { this._Setting.ChannelID = value; } }

                [Category   ("��{�ݒ�")]
                [DisplayName("�R�����gAPI�g�[�N��")]
                [Description("FC2���C�u�̃��[�U�[�ݒ�y�[�W��\n���R�����gAPI�̐ݒ�\n���A�N�Z�X�g�[�N��")]
                public string Token { get { return this._Setting.Token; } set { this._Setting.Token = value; } }

                [Category   ("�ڍאݒ�")]
                [DisplayName("�R�����g�`��")]
                [Description("�R�����g�̌`��\n%1$s: ���O\n%2$s: �R�����g")]
                [Browsable(false)]
                public string CommentRegExp { get { return this._Setting.CommentRegExp; } set { this._Setting.CommentRegExp = value; } }

                [Category   ("�ڍאݒ�")]
                [DisplayName("�`�b�v�R�����g�`��")]
                [Description("�`�b�v�R�����g�̌`��\n%1$s: ���O\n%1$d: �`�b�v�|�C���g��")]
                [Browsable(false)]
                public string TipRegExp { get { return this._Setting.TipRegExp; } set { this._Setting.TipRegExp = value; } }

                [Category   ("�ڍאݒ�")]
                [DisplayName("�v���[���g�R�����g�`��")]
                [Description("�v���[���g�R�����g�̌`��\n%1$s: ���O\n%2$s: �v���[���g��\n%1$d: �v���[���g�|�C���g��")]
                [Browsable(false)]
                public string GiftRegExp { get { return this._Setting.GiftRegExp; } set { this._Setting.GiftRegExp = value; } }

                /* ISettingPropertyGrid�ł͐ݒ��ʂł̕\�����ڂ��w��ł��܂��B
                [Category   ("����")]
                [DisplayName("�\����")]
                [Description("������")]
                [DefaultValue(0)]        //�f�t�H���g�l�F�����\������Ȃ�����
                [Browsable(false)]       //PropertyGrid�ŕ\�����Ȃ�
                [ReadOnly(true)]         //PropertyGrid�œǂݍ��ݐ�p�ɂ���
                string  �t�@�C���I��     ��[Editor(typeof(System.Windows.Forms.Design.FolderNameEditor),       typeof(System.Drawing.Design.UITypeEditor))]
                string  �t�H���_�I��     ��[Editor(typeof(System.Windows.Forms.Design.FileNameEditor),         typeof(System.Drawing.Design.UITypeEditor))]
                string  �����s��������� ��[Editor(typeof(System.ComponentModel.Design.MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
                */
            }
        }

        #endregion
    }
}
