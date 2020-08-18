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
        //設定
        private Settings_FC2           _Settings;
        private SettingFormData_FC2    _SettingFormData;
        //設定ファイルの保存場所
        private string                 _SettingFile = Base.CallAsmPath + Base.CallAsmName + ".setting";
        //タイマ
        private System.Threading.Timer _Timer;
        private ToolStripButton        _Button;
        private ToolStripSeparator     _Separator;
        private bool _IconStatus;
        private bool _SettingStatus;
        private long _LastCommentIndex;
        private long _LastCommentIndexTmp;
        private long _AnonymousIndex;
        private Dictionary<string, long> _AnonymousHash;
        private Dictionary<int, string> _GiftItems;

        #endregion


        #region ■IPluginメンバの実装

        public string           Name            { get { return "FC2ライブ読み上げ API ver"; } }
        public string           Version         { get { return "2020/08/18版"; } }
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
            this._SettingStatus = false;
            this.ResetCommentIndex();
            this._GiftItems = new Dictionary<int, string>(){
                {0, "風船"},
                {1, "ハート"},
                {2, "ダイヤ"},
                {3, "ドーナツ"},
                {4, "ニンジャ"},
                {5, "キャンディ"},
                {6, "クラッカー"},
                {7, "花火"},
                {8, "キッス"},
                {9, "いいね"},
                {10, "車"},
                {11, "さかな"},
                {12, "UFO"},
                {13, "シャンパン"},
                {999, "オチャコ"}
            };

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
            if (!this._SettingStatus) {
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
            this._AnonymousIndex = 0;
            this._AnonymousHash = new Dictionary<string, long>();
        }

        internal void SetSettingStatus(string token, string id) {
            this._SettingStatus = (token.Length == 16 && id != "");
        }

        private long getAnonymousIndex(string hash) {
            if (this._AnonymousHash.ContainsKey(hash)) {
                return this._AnonymousHash[hash];
            }
            this._AnonymousHash.Add(hash, ++this._AnonymousIndex);
            return this._AnonymousIndex;
        }

        private string getGiftItemName(int id) {
            if(this._GiftItems.ContainsKey(id)) {
                return this._GiftItems[id];
            }
            return "何か";
        }

        private void AddCommentTalk(long lastCommentIndex) {
            if(this._LastCommentIndexTmp != lastCommentIndex) {
                this._LastCommentIndexTmp = lastCommentIndex;
                try {
                    CommentData commentData = sendRequest(lastCommentIndex);
                    this._LastCommentIndexTmp = -2;
                    switch (commentData.status) {
                        case 0:
                            if (lastCommentIndex >= commentData.last_comment_index) {
                                break;
                            }
                            this._LastCommentIndex = commentData.last_comment_index;
                            if (lastCommentIndex >= 0) {
                                foreach (Comment comment in commentData.comments) {
                                    if (comment.anonymous == 1) {
                                        long anonymousIndex = this.getAnonymousIndex(comment.hash);
                                        comment.user_name = this._Settings.AnonymousString.Replace("%d", anonymousIndex.ToString());
                                    }
                                    string commentText;
                                    if(comment.system_comment == null) {
                                        if (!this._Settings.CommentFlg || comment.ng_comment_user == 1 || comment.comment.Length <= 0) {
                                            continue;
                                        }
                                        commentText = this._Settings.CommentString.Replace("%1$s", comment.user_name).Replace("%2$s", comment.comment);
                                    } else {
                                        switch (comment.system_comment.type) {
                                            case "gift":
                                                if (!this._Settings.GiftFlg) {
                                                    continue;
                                                }
                                                if (comment.system_comment.tip_amount > 0) {
                                                    commentText = this._Settings.GiftPointString;
                                                } else {
                                                    commentText = this._Settings.GiftString;
                                                }
                                                commentText = commentText.Replace("%1$s", comment.user_name)
                                                    .Replace("%2$s", this.getGiftItemName(comment.system_comment.gift_id))
                                                    .Replace("%1$d", comment.system_comment.tip_amount.ToString())
                                                    .Replace("%2$d", comment.system_comment.tip_total.ToString());
                                                break;
                                            case "tip":
                                                if (!this._Settings.TipFlg) {
                                                    continue;
                                                }
                                                commentText = this._Settings.TipString.Replace("%1$s", comment.user_name)
                                                    .Replace("%1$d", comment.system_comment.tip_amount.ToString())
                                                    .Replace("%2$d", comment.system_comment.tip_total.ToString());
                                                break;
                                            default:
                                                continue;
                                        }
                                    }
                                    Pub.AddTalkTask(commentText, -1, -1, VoiceType.Default);
                                }
                            } else {
                                this.ChangeIcon(true);
                                this._SettingStatus = true;
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
                        case 99:
                            if (!this._Settings.Discard99Flg) {
                                this._Settings.TimeSignal = false;
                                Pub.AddTalkTask("FC2ライブの読み上げを終了します", -1, -1, VoiceType.Default);
                                Pub.AddTalkTask("エラーが発生しました", -1, -1, VoiceType.Default);
                            }
                            break;
                        case 98:
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
            public int ng_comment_user;
            public SystemComment system_comment;
        }

        public class SystemComment {
            public string type;
            public int gift_id;
            public int tip_amount;
            public int tip_total;
        }

        // 設定クラス（設定画面表示・ファイル保存を簡略化。publicなメンバだけ保存される。XmlSerializerで処理できるクラスのみ使用可。）
        public class Settings_FC2 : SettingsBase {
            //保存される情報（設定画面からも参照される）
            public bool TimeSignal = true;

            public string ChannelID = "";
            public string Token = "";
            public string AnonymousString = "匿名(%d)";
            public string CommentString = "%2$s";
            public string TipString = "%1$dポイントを%1$sさんがチップしました";
            public string GiftString = "%2$sを%1$sさんがプレゼントしました";
            public string GiftPointString = "%2$sを%1$sさんがプレゼントしました";
            public bool CommentFlg = true;
            public bool TipFlg = true;
            public bool GiftFlg = true;
            public bool Discard99Flg = false;

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
                this.Plugin.SetSettingStatus(this.Token, this.ChannelID);
                this.Plugin.ResetCommentIndex();
            }

            //当オブジェクトからGUIなどへの反映(設定ロード時・設定更新時に呼ばれる)
            public override void WriteSettings() {
                this.Plugin.SetSettingStatus(this.Token, this.ChannelID);
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

                [Category   ("01)基本設定")]
                [DisplayName("01)チャンネルID")]
                [Description("読み上げ対象のチャンネルID\nhttps://live.fc2.com/65177747/ なら 65177747 の部分を入力")]
                public string ChannelID { get { return this._Setting.ChannelID; } set { this._Setting.ChannelID = value; } }

                [Category   ("01)基本設定")]
                [DisplayName("02)コメントAPIトークン")]
                [Description("FC2ライブのユーザー設定ページ(https://live.fc2.com/profile_edit/)の\n→コメントAPIの設定\n→アクセストークンを入力")]
                public string Token { get { return this._Setting.Token; } set { this._Setting.Token = value; } }

                [Category   ("01)基本設定")]
                [DisplayName("03)読み上げを有効にする")]
                [Description("false にすると、読み上げ自体を停止します")]
                [DefaultValue(true)]
                public bool TimeSignal { get { return this._Setting.TimeSignal; } set { this._Setting.TimeSignal = value; } }

                [Category   ("01)基本設定")]
                [DisplayName("04)通常コメントの読み上げを有効にする")]
                [Description("false にすると、通常コメントの読み上げを停止します")]
                [DefaultValue(true)]
                public bool CommentFlg { get { return this._Setting.CommentFlg; } set { this._Setting.CommentFlg = value; } }

                [Category   ("01)基本設定")]
                [DisplayName("05)チップの読み上げを有効にする")]
                [Description("false にすると、チップコメントの読み上げを停止します")]
                [DefaultValue(true)]
                public bool TipFlg { get { return this._Setting.TipFlg; } set { this._Setting.TipFlg = value; } }

                [Category   ("01)基本設定")]
                [DisplayName("06)ギフトの読み上げを有効にする")]
                [Description("false にすると、ギフトコメントの読み上げを停止します")]
                [DefaultValue(true)]
                public bool GiftFlg { get { return this._Setting.GiftFlg; } set { this._Setting.GiftFlg = value; } }

                [Category   ("02)詳細設定")]
                [DisplayName("01)匿名の表記")]
                [Description("%d ： 匿名番号\n\n例）匿名(%d) \n→ 匿名(1)")]
                [DefaultValue("匿名(%d)")]
                public string AnonymousString { get { return this._Setting.AnonymousString; } set { this._Setting.AnonymousString = value; } }

                [Category   ("02)詳細設定")]
                [DisplayName("02)コメントの形式")]
                [Description("%1$s ： [名前]\n%2$s ： [コメント]\n\n例）%1$sさん %2$s \n→ [名前]さん [コメント]")]
                [DefaultValue("%2$s")]
                public string CommentString { get { return this._Setting.CommentString; } set { this._Setting.CommentString = value; } }

                [Category   ("02)詳細設定")]
                [DisplayName("03)チップコメントの形式")]
                [Description("%1$s ： [名前]\n%1$d ： [チップポイント数]\n%2$d ： [チップポイントトータル]\n\n例）%1$sさんが%1$dポイントチップしました トータル%2$dポイント \n→ [名前]さんが[チップポイント数]ポイントチップしました トータル[チップポイントトータル]ポイント")]
                [DefaultValue("%1$dポイントを%1$sさんがチップしました")]
                public string TipString { get { return this._Setting.TipString; } set { this._Setting.TipString = value; } }

                [Category   ("02)詳細設定")]
                [DisplayName("04)プレゼントコメントの形式")]
                [Description("%1$s ： [名前]\n%2$s ： [プレゼント名]\n\n例）%1$sさんが%2$sをプレゼントしました \n→ [名前]さんが[プレゼント名]をプレゼントしました")]
                [DefaultValue("%2$sを%1$sさんがプレゼントしました")]
                public string GiftString { get { return this._Setting.GiftString; } set { this._Setting.GiftString = value; } }

                [Category   ("02)詳細設定")]
                [DisplayName("05)チップ付きプレゼントの形式")]
                [Description("%1$s ： [名前]\n%2$s: [プレゼント名]\n%1$d ： [チップポイント数]\n%2$d ： [チップポイントトータル]\n\n例）%1$sさんが%1$dポイントの%2$sをプレゼントしました トータル%2$dポイント \n→ [名前]さんが[プレゼント名]をプレゼントしました トータル[チップポイントトータル]ポイント")]
                [DefaultValue("%2$sを%1$sさんがプレゼントしました")]
                public string GiftPointString { get { return this._Setting.GiftPointString; } set { this._Setting.GiftPointString = value; } }

                [Category   ("03)エラー設定")]
                [DisplayName("01)99番のエラーを無視する")]
                [Description("必要な時のみtrueにしてください")]
                [DefaultValue(false)]
                public bool Discard99Flg { get { return this._Setting.Discard99Flg; } set { this._Setting.Discard99Flg = value; } }

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
