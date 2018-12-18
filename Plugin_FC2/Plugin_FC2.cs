//プラグインのファイル名は、「Plugin_*.dll」という形式にして下さい。
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
        #region ■フィールド

        private Settings_FC2           _Settings;                                                       //設定
        private SettingFormData_FC2    _SettingFormData;
        private string                 _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting"; //設定ファイルの保存場所
        private System.Threading.Timer _Timer;                                                          //タイマ
        private ToolStripButton        _Button;
        private ToolStripSeparator     _Separator;
        private bool _IconStatus;
        private bool _SettingStatus;
        private long _LastCommentIndex;
        private long _LastCommentIndexTmp;

        #endregion


        #region ■IPluginメンバの実装

        public string           Name            { get { return "FC2ライブ読み上げ API ver"; } }

        public string           Version         { get { return "2018/12/16版"; } }

        public string           Caption         { get { return "FC2ライブのコメントを読み上げます。"; } }

        //プラグインの設定画面情報（設定画面が必要なければnullを返す）
        public ISettingFormData SettingFormData { get { return _SettingFormData; } }

        //プラグイン開始時処理
        public void Begin() {
            //設定ファイル読み込み
            this._Settings = new Settings_FC2(this);
            this._Settings.Load(this._SettingFile);
            this._SettingFormData = new SettingFormData_FC2(this._Settings);
            this._IconStatus = true;
            this._SettingStatus = true;
            this._LastCommentIndex = -1;
            this._LastCommentIndexTmp = -2;

            //タイマー登録
            this._Timer = new System.Threading.Timer(this.Timer_Event, null, 0, 1000);

            //画面にボタンとセパレータを追加
            this._Separator = new ToolStripSeparator();
            Pub.ToolStrip.Items.Add(this._Separator);
            this._Button = new ToolStripButton(Properties.Resources.ImgFC2Off);
            this._Button.ToolTipText = "FC2ライブ読み上げ ON/OFF";
            this._Button.Click      += this.Button_Click;
            Pub.ToolStrip.Items.Add(this._Button);
        }

        //プラグイン終了時処理
        public void End() {
            //設定ファイル保存
            this._Settings.Save(_SettingFile);

            //タイマ開放
            if (this._Timer != null) {
                this._Timer.Dispose();
                this._Timer = null;
            }

            //画面からボタンとセパレータを削除
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


        #region ■メソッド・イベント処理

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

        //タイマーイベント
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
                                //Pub.AddTalkTask("FC2ライブの読み上げを開始します", -1, -1, VoiceType.Default);
                            }
                            break;
                        case 10:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("APIアクセスパラメーターが足りていません", -1, -1, VoiceType.Default);
                            break;
                        case 11:
                            this._Settings.TimeSignal = false;
                            this._SettingStatus = false;
                            Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("トークンが正しくありません", -1, -1, VoiceType.Default);
                            break;
                        case 12:
                            this._Settings.TimeSignal = false;
                            this._SettingStatus = false;
                            Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("チャンネルが見つかりません", -1, -1, VoiceType.Default);
                            break;
                        case 13:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("視聴していないチャンネルです", -1, -1, VoiceType.Default);
                            break;
                        case 98:
                        case 99:
                        default:
                            this._Settings.TimeSignal = false;
                            Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                            Pub.AddTalkTask("エラーが発生しました", -1, -1, VoiceType.Default);
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


        #region ■クラス・構造体

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

        // 設定クラス（設定画面表示・ファイル保存を簡略化。publicなメンバだけ保存される。XmlSerializerで処理できるクラスのみ使用可。）
        public class Settings_FC2 : SettingsBase {
            //保存される情報（設定画面からも参照される）
            public bool TimeSignal = true;

            public string ChannelID;
            public string Token;
            public string CommentRegExp;
            public string TipRegExp;
            public string GiftRegExp;

            //作成元プラグイン
            internal Plugin_FC2 Plugin;

            //コンストラクタ
            public Settings_FC2() {
            }

            //コンストラクタ
            public Settings_FC2(Plugin_FC2 pFC2) {
                this.Plugin = pFC2;
            }

            //GUIなどから当オブジェクトの読み込み(設定セーブ時・設定画面表示時に呼ばれる)
            public override void ReadSettings() {
                this.Plugin.SetSettingStatus();
                this.Plugin.ResetCommentIndex();
            }

            //当オブジェクトからGUIなどへの反映(設定ロード時・設定更新時に呼ばれる)
            public override void WriteSettings() {
                this.Plugin.SetSettingStatus();
                this.Plugin.ResetCommentIndex();
            }
        }

        // 設定画面表示用クラス（設定画面表示・ファイル保存を簡略化。publicなメンバだけ保存される。XmlSerializerで処理できるクラスのみ使用可。）
        public class SettingFormData_FC2 : ISettingFormData {
            Settings_FC2 _Setting;

            public string       Title     { get { return _Setting.Plugin.Name; } }
            public bool         ExpandAll { get { return false; } }
            public SettingsBase Setting   { get { return _Setting; } }

            public SettingFormData_FC2(Settings_FC2 setting) {
                this._Setting = setting;
                this.PBase    = new SBase(this._Setting);
            }

            //設定画面で表示されるクラス(ISettingPropertyGrid)
            public SBase PBase;
            public class SBase : ISettingPropertyGrid {
                Settings_FC2 _Setting;
                public SBase(Settings_FC2 setting) { this._Setting = setting; }
                public string GetName() { return "設定"; }

                [Category   ("基本設定")]
                [DisplayName("読み上げを有効にする")]
                public bool TimeSignal { get { return this._Setting.TimeSignal; } set { this._Setting.TimeSignal = value; } }

                [Category   ("基本設定")]
                [DisplayName("チャンネルID")]
                [Description("読み上げ対象のチャンネルID\nhttps://live.fc2.com/65177747/ なら 65177747")]
                public string ChannelID { get { return this._Setting.ChannelID; } set { this._Setting.ChannelID = value; } }

                [Category   ("基本設定")]
                [DisplayName("コメントAPIトークン")]
                [Description("FC2ライブのユーザー設定ページの\n→コメントAPIの設定\n→アクセストークン")]
                public string Token { get { return this._Setting.Token; } set { this._Setting.Token = value; } }

                [Category   ("詳細設定")]
                [DisplayName("コメント形式")]
                [Description("コメントの形式\n%1$s: 名前\n%2$s: コメント")]
                [Browsable(false)]
                public string CommentRegExp { get { return this._Setting.CommentRegExp; } set { this._Setting.CommentRegExp = value; } }

                [Category   ("詳細設定")]
                [DisplayName("チップコメント形式")]
                [Description("チップコメントの形式\n%1$s: 名前\n%1$d: チップポイント数")]
                [Browsable(false)]
                public string TipRegExp { get { return this._Setting.TipRegExp; } set { this._Setting.TipRegExp = value; } }

                [Category   ("詳細設定")]
                [DisplayName("プレゼントコメント形式")]
                [Description("プレゼントコメントの形式\n%1$s: 名前\n%2$s: プレゼント名\n%1$d: プレゼントポイント数")]
                [Browsable(false)]
                public string GiftRegExp { get { return this._Setting.GiftRegExp; } set { this._Setting.GiftRegExp = value; } }

                /* ISettingPropertyGridでは設定画面での表示項目を指定できます。
                [Category   ("分類")]
                [DisplayName("表示名")]
                [Description("説明文")]
                [DefaultValue(0)]        //デフォルト値：強調表示されないだけ
                [Browsable(false)]       //PropertyGridで表示しない
                [ReadOnly(true)]         //PropertyGridで読み込み専用にする
                string  ファイル選択     →[Editor(typeof(System.Windows.Forms.Design.FolderNameEditor),       typeof(System.Drawing.Design.UITypeEditor))]
                string  フォルダ選択     →[Editor(typeof(System.Windows.Forms.Design.FileNameEditor),         typeof(System.Drawing.Design.UITypeEditor))]
                string  複数行文字列入力 →[Editor(typeof(System.ComponentModel.Design.MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
                */
            }
        }

        #endregion
    }
}
