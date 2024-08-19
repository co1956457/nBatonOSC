// http://c-loft.com/blog/?p=719            この記事を参考に作成                Webの公開情報
// https://github.com/chinng-inta           運営コメントの条件分岐等参考にした   MIT License
// https://github.com/ValdemarOrn/SharpOSC  部分利用                            MIT License
//
// SPDX-License-Identifier: MIT
//
// This plugin sends data from NCV to VirtualCast by OSC.
// NCV から VirtualCast へ OSC で送信するプラグイン
//
// 20230416 v1.0 Taki (co1956457)
// 20240819 v1.1 Taki Any CPU (x86, x64), Plugin.dll and NicoLibrary.dll from x64 NCV

using System;
using System.IO;                    // File, Directory
using System.Windows.Forms;         // MessageBox
using System.Linq;                  // Last
using System.Text;                  // StringBuilder
using System.Collections.Generic;   // List

// Add References 参照追加
// \NiconamaCommentViewer\Plugin.dll, NicoLibrary.dll  (x64)
using Plugin;

namespace nBatonOSC
{
    public class Class1 : IPlugin
    {
        private IPluginHost _host = null;

        #region IPlugin メンバ

        // Form用
        private Form1 _form1;

        // ファイル存在確認エラー用
        int fileExist;

        // プラグインの状態
        // transferMode
        //  0: 転送しない Off
        //  1: スタジオ Studio (運営コメント Operator comments)
        //  2: ルーム Room (全コメント All comments)
        public int transferMode;

        // 起動時にだけファイルから転送モードを読み込む
        private int initialRead = 0;

        // カレントディレクトリ = プラグインディレクトリ（AppData\Roaming）
        string curDirectory = Environment.CurrentDirectory;
        // 設定ファイル のパス
        string readPath;

        /// <summary>
        /// プラグインの名前
        /// </summary>
        public string Name
        {
            // get { throw new NotImplementedException(); }
            get { return "nBatonOSC " + Version + " 設定 (Settings)"; }
        }

        /// <summary>
        /// プラグインのバージョン
        /// </summary>
        public string Version
        {
            // get { throw new NotImplementedException(); }
            get { return "v1.1"; }
        }

        /// <summary>
        /// プラグインの説明
        /// </summary>
        public string Description
        {
            // get { throw new NotImplementedException(); }
            get { return "NCV から VirtualCast へ OSC で送信"; }
        }

        /// <summary>
        /// プラグインのホスト
        /// </summary>
        public IPluginHost Host
        {
            get
            {
                // throw new NotImplementedException();
                return this._host;
            }
            set
            {
                // throw new NotImplementedException();
                this._host = value;
            }
        }

        /// <summary>
        /// アプリケーション起動時にプラグインを自動実行するかどうか
        /// </summary>
        public bool IsAutoRun
        {
            // get { throw new NotImplementedException(); }
            get { return true; }
        }

        /// <summary>
        /// IsAutoRunがtrueの場合、アプリケーション起動時に自動実行される
        /// </summary>
        public void AutoRun()
        {
            // ファイルの存在確認
            fileExist = fileExistError();

            if (fileExist == 0) // 問題なし
            {
                initialRead = 1;
            }
            else // 問題あり
            {
                ShowFileExistError(fileExist);
            }

            // コメント受信時のイベントハンドラ追加
            _host.ReceivedComment += new ReceivedCommentEventHandler(_host_ReceivedComment);

            // 放送接続イベントハンドラ追加
            _host.BroadcastConnected += new BroadcastConnectedEventHandler(_host_BroadcastConnected);

            // 放送切断イベントハンドラ追加
            _host.BroadcastDisConnected += new BroadcastDisConnectedEventHandler(_host_BroadcastDisConnected);

            // 終了時イベントハンドラ追加
            _host.MainForm.FormClosing += MainForm_FormClosing;
        }

        /// <summary>
        /// プラグイン→ nBatonOSC 設定 (Settings) を選んだ時
        /// </summary>
        public void Run()
        {
            if (_form1 == null || _form1.IsDisposed)
            {
                //フォームの生成
                _form1 = new Form1(this);
                _form1.Text = Name;
                _form1.Show();
                _form1.FormClosed += new FormClosedEventHandler(_form1_FormClosed);
            }
            else
            {
                _form1.Focus();
            }
        }

        /// <summary>
        /// 閉じるボタン☒を押した時
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            int int_close = -1;
            SendOscMode(int_close);
        }

        /// <summary>
        /// コメントを受信したら OSC で送信
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _host_ReceivedComment(object sender, ReceivedCommentEventArgs e)
        {
            if(transferMode > 0) // 稼働中
            {
                // 受信したコメント数を取り出す
                int count = e.CommentDataList.Count;
                if (count == 0)
                {
                    return;
                }
                // 最新のコメントデータを取り出す
                NicoLibrary.NicoLiveData.LiveCommentData commentData = e.CommentDataList[count - 1];

                // default: Operator 既定値は（運営）
                string name = "（運営）";

                // 今後の VCI 開発はコメントバトンを介さず直接データを受信する形式になることが予想される。
                // MultiCommentViewer との関係もあり、OSC 対応版を機に commentSource の文字列を変更。
                // 一般コメント         : Nicolive
                // 184コメント          : Nicolive184
                // 運営コメント(既定値) : NicoliveOperator
                // 特定の運営コメント   : NicoliveAd, NicoliveInfo, NicoliveGift, NicoliveSpi, NicoliveEmotion
                //
                // ※以前のプラグインでは、運営コメントの commentSource は "NCV" としていた。
                // 　(当初は NCV から運営コメントを送るだけだった名残）。
                // 　既存のコメントバトンを利用した VCI に影響が出ないよう、
                // 　コメントバトン (OSC) の main.lua 側で "NCV" に戻す等対応。

                // コメント文字列を取り出す
                (string comment, string commentSource) = EditComment(commentData.Comment);

                // 運営コメントの判定式
                bool isOperator = (((commentData.PremiumBits & NicoLibrary.NicoLiveData.PremiumFlags.ServerComment) == NicoLibrary.NicoLiveData.PremiumFlags.ServerComment));

                // 0 は転送しない
                // 1の時は運営コメントのみ転送　2の時は全コメント 
                if (((transferMode == 1) && isOperator) || (transferMode == 2))
                {
                    // 一般コメント
                    if (!isOperator)
                    {
                        // UserId から名前と 184 判定結果を取得
                        bool anonymous;
                        (name, anonymous) = NameFromXML(commentData.UserId, commentData.Name);
                        // 三項演算子
                        // = 条件 ? 真の場合の値 : 偽の場合の値; (直接値が返る)
                        // anonymous = true の時 "Nicolive184", false の時 "Nicolive"
                        // if (anonymous)
                        // {
                        //      commentSource = "Nicolive184";
                        // }
                        // else
                        // {
                        //      commentSource = "Nicolive";
                        // }
                        commentSource = (anonymous) ? "Nicolive184" : "Nicolive";                         
                    }
                    // OSC で送るデータを引き渡す
                    SendOscComment(comment, name, commentSource, transferMode);
                }

                /*transferModeでの分岐（※動作理解のためにコメントアウトで残しておく）
                if (transferMode == 0) // 転送しない
                {
                    // do nothing
                }
                else if (transferMode == 1) // スタジオモード ニコ生運営コメのみ転送　一般コメ転送しない
                {
                    // 運営コメントのみ送信
                    if (isOperator)
                    {
                        SendOscComment(comment, name, commentSource, transferMode);
                    }
                }
                else if (transferMode == 2)// 全コメント (transferMode ==2)
                {
                    // 一般コメントの時
                    if (!isOperator)
                    {
                        name = NameFromXML(commentData.UserId);
                        commentSource = "Nicolive";
                    }
                    SendOscComment(comment, name, commentSource, transferMode);
                }
                */

            }
            else
            {
                // do nothing
            }
        }

        /// <summary>
        /// コメント等各種情報を OSC で送信
        /// SharpOSC (MIT license) を部分利用
        /// https://github.com/ValdemarOrn/SharpOSC
        /// </summary>
        /// 
        static void SendOscComment(string commentOSC, string nameOSC, string commentSourceOSC, int transferModeOSC)
        {
            try
            {
                Encoding utf8 = Encoding.UTF8;
                byte[] blob_comment = utf8.GetBytes(commentOSC);
                byte[] blob_name = utf8.GetBytes(nameOSC);
                string str_commentSource = commentSourceOSC;
                int int_transferMode = transferModeOSC;

                var sender = new UDPSender("127.0.0.1", 19100);
                var message = new OscMessage("/vci/baton/comment", blob_comment, blob_name, str_commentSource, int_transferMode);

                sender.Send(message);
            }
            //catch (Exception error)
            catch (Exception)
            {
                //MessageBox.Show(error.ToString());  // just in case
            }
        }

        static void SendOscMode(int modeOSC)
        {
            try
            {
                int int_transferMode = modeOSC;

                var sender = new UDPSender("127.0.0.1", 19100);
                var message = new OscMessage("/vci/baton/mode", int_transferMode);

                sender.Send(message);
            }
            //catch (Exception error)
            catch (Exception)
            {
                // MessageBox.Show(error.ToString());  // just in case
            }
        }

        /// <summary>
        /// 放送接続時イベントハンドラ
        /// </summary>
        void _host_BroadcastConnected(object sender, EventArgs e)
        {
            // 転送モードを送信
            SendOscMode(transferMode);
        }

        /// <summary>
        /// 放送切断時イベントハンドラ
        /// </summary>
        void _host_BroadcastDisConnected(object sender, EventArgs e)
        {
            // do nothing
        }

         /// <summary>
        /// ファイルの存在確認
        /// </summary>
        int fileExistError()
        {
            // 値を返す用
            int returnInt;
            // 設定ファイル名
            readPath = curDirectory + "\\nBatonOSC.txt";

            // ファイルの存在確認
            if (File.Exists(readPath)) // 設定ファイルあり
            {
                // 行ごとのに、テキストファイルの中身をすべて読み込む
                string[] lines = File.ReadAllLines(readPath);

                if (initialRead == 0) // 起動時のみファイルから転送モード読み込み
                {
                    // transferMode
                    //  0: 転送しない Off
                    //  1: スタジオ Studio (運営コメント Operator comments)
                    //  2: ルーム Room (全コメント All comments)
                    //
                    if (lines[0] == "0" || lines[0] == "1" || lines[0] == "2")
                    {
                        transferMode = int.Parse(lines[0]);
                    }
                    else
                    {
                        transferMode = 1; // initial setting
                    }
                }
                SendOscMode(transferMode);
                returnInt = 0;
            }
            else
            {
                try
                {
                    // 初導入時等、設定ファイルがない時
                    transferMode = 1;
                    File.WriteAllText(readPath, transferMode.ToString());
                    SendOscMode(transferMode);
                    returnInt = 0;
                }
                catch (Exception)
                {
                    returnInt = 1;
                }
            }
            return returnInt;
        }

        /// <summary>
        /// エラー表示
        /// </summary>
        void ShowFileExistError(int errorNumber)
        {
            if (errorNumber == 1)
            {
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n設定ファイルがありません。\nThere is no setting file.\n\n1. C:\\Users\\%ユーザー名%\\AppData\\Roaming\\posite-c\\NiconamaCommentViewer\\nBatonOSC.txt を作成してください(※「UserSetting.xml」と同じフォルダー。NCVのインストール先を変えた人は自分の環境に合わせてください)。\n   Please create the text file \"nBatonOSC.txt\" (The directory has \"UserSetting.xml\" If you changed NCV install directory, default directory might be changed...).\n\n2. nBatonOSC.txt に 半角で「1」を書いて保存してください。\n   Please write the number '1' in the text file.\n\n3. NCVを立ち上げなおしてください。\n   Please reboot NCV.", "nBatonOSC エラー error");
            }
        }

        //フォームが閉じられた時のイベントハンドラ
        void _form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            int old_transferMode = transferMode;
            transferMode = _form1.tMode;

            if (old_transferMode != transferMode)
            {
                // 設定ファイルにモードを保存
                File.WriteAllText(readPath, transferMode.ToString());
                SendOscMode(transferMode);
            }

            //フォームが閉じられた時のイベントハンドラ削除
            _form1.FormClosed -= _form1_FormClosed;
            _form1 = null;
        }


        /// <summary>
        /// comment.UserID に対応する名前があれば返す
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        private (string, bool) NameFromXML(string userID, string commentDataName)
        {
            bool anonymous = !userID.All(char.IsDigit);
            // 生 ID でニコニコのユーザー名が取得できていなかった時は "（生ID）"
            // 生 ID でニコニコのユーザー名が取得できていたらニコニコのユーザー名
            // 184 の時は ID
            string userName = userID;
            if (!anonymous)
            {
                userName = (commentDataName == "") ? "（生ID）" : commentDataName;
            }

            // Plugin_Doc.chm を "GetUserSettingInPlugin" で検索
            UserSettingInPlugin userSetting = _host.GetUserSettingInPlugin();
            
            List<UserSettingInPlugin.UserData> userDataList = userSetting.UserDataList;
            
            UserSettingInPlugin.UserData userData = null;

            // userDataList の中に userID があるかどうか←固定ハンドルネーム(コテハン)が登録されているかどうか
            // コテハン登録されていれば (184 でも) コテハン名で上書き
            // 複数回登録されている可能性があるので新しいデータ(後ろ)から見ていく
            for (int i = userDataList.Count - 1; i >= 0; i--)
            {
                if (userDataList[i].UserId == userID)
                {
                    userData = userDataList[i];
                    userName = userData.NickName; // 登録されているコテハン名
                    break;
                }
            }
            return (userName, anonymous);
        }

        /// <summary>
        /// 運営コメントを編集
        /// </summary>
        private (string, string) EditComment(string message)
        {
            string cmntSrc = "NicoliveOperator";
            string msg = message.Replace("\"", "\\\"").Replace("\'", "\\\'");
            string[] str = msg.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            switch (str[0])
            {
                case "/nicoad":
                    cmntSrc = "NicoliveAd";
                    // ※フォーマットが変わった
                    // 旧 「\"」 前5削除 後5削除
                    // /nicoad {\"totalAdPoint\":12200,\"message\":\"Takiさんが600ptニコニ広告しました「おすすめの放送です」\",\"version\":\"1\"}
                    //
                    // 新 「"」で Split 後、文字列の最後の「\」削除
                    // /nicoad {\"version\":\"1\",\"totalAdPoint\":12200,\"message\":\"【広告貢献1位】Takiさんが100ptニコニ広告しました\"}
                    // /nicoad {\"version\":\"1\",\"totalAdPoint\":12200,\"message\":\"Takiさんが1000ptニコニ広告しました「おすすめの放送です」\"}
                    //

                    // 「"」でメッセージを分割 (ニコニコのニックネームには「"」「'」が使えない)
                    string[] nicoadCmnt = msg.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // 広告メッセージ
                    string adMessage = nicoadCmnt[9];
                    
                    // 最後の文字「\」を削除
                    adMessage = adMessage.TrimEnd('\\');

                    // 特殊文字変換
                    adMessage = adMessage.Replace("\n", "").Replace("\r", "");
                    adMessage = adMessage.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
                    adMessage = adMessage.Replace("$", "＄").Replace("/", "／").Replace(",", "，");

                    msg = adMessage;
                    break;
                case "/info":
                    cmntSrc = "NicoliveInfo";
                    // 
                    // /info 1 市場に文字シールワッペン　ひらがな　紺　ふが登録されました。
                    // /info 2 1人がコミュニティに参加しました。
                    // /info 2 1人（プレミアム1人）がコミュニティをフォローしました。
                    // /info 3 30分延長しました
                    // /info 4 
                    // /info 5 
                    // /info 6 観測地域:ニコ県沿岸北部　震度:5弱　発生時間:2099年 7月 5日 07時 42分
                    // /info 7 震源地:ニコ県沖　震度:5弱　マグニチュード:5.8　発生時間:2099年 7月 5日 07時 42分
                    // /info 8 第1位にランクインしました
                    // /info 10 ニコニ広告枠から1人が来場しました
                    // /info 10 「雑談」が好きな1人が来場しました
                    // 
                    // 「"」なし
                    // /info 6,7 中に半角スペースあり
                    // 
                    int infoNumber = 0;
                    try
                    {
                        infoNumber = int.Parse(str[1]);
                    }
                    catch (Exception)
                    {
                        // do nothing
                    }
                    msg = (infoNumber < 10) ? msg.Remove(0, 8) : msg.Remove(0, 9); // 先頭8,9文字「/info * 」削除。

                    break;
                case "/gift":
                    cmntSrc = "NicoliveGift";
                    // 2****7 はニコニコの ID
                    // 通常ギフト、イベントギフト
                    // /gift seed 2****7 \"Taki\" 50 \"\" \"ひまわりの種\"
                    // /gift giftevent_niku 2****7 \"Taki\" 90 \"\" \"肉\"
                    // /gift giftevent_yasai 2****7 \"Taki\" 20 \"\" \"野菜\"
                    // /gift giftevent_mashumaro 2****7 \"Taki\" 10 \"\" \"焼きマシュマロ\"
                    // /gift giftevent_mashumaro NULL \"名無し\" 10 \"\" \"焼きマシュマロ\"
                    //
                    // Vギフトランキングあり
                    // /gift vcast_ocha 2****7 \"Taki\" 300 \"\" \"お茶\" 1
                    // 【ギフト貢献1位】Takiさんがギフト「お茶（300pt）」を贈りました
                    // 
                    // /gift vcast_free_shell 2****7 \"Taki\" 0 \"\" \"貝がら（６種ランダム）\" 2
                    // 【ギフト貢献2位】Takiさんがギフト「貝がら（6種ランダム）（0pt）」を贈りました
                    // 
                    // /gift vcast_free_shell NULL \"名無し\" 0 \"\" \"貝がら（6種ランダム）\"
                    // 名無しさんがギフト「貝がら（6種ランダム）（0pt）」を贈りました
                    // 
                    // 名前に半角スペースが入っている人がいる
                    // /gift vcast_ocha 2****7 \"Ta /,\ki(SPSLCY)\" 0 \"\" \"貝がら（6種ランダム）\" 1
                    //
                    // 先頭から1回目の「\"」+ 1 から
                    // 後ろから5回目の「\"」- 1 まで (「\" 」は 名無しNULL のときずれる)
                    // 名前に半角スペースが入っていても大丈夫
                    // 「"」「'」は全角に変換
                    // 
                    // string user = msg.Substring(fmNum, toNum); 落ちる
                    // 
                    // msg = /gift vcast_ocha 2****7 \"Taki\" 300 \"\" \"お茶\" 1
                    // msg = /gift vcast_ocha 2****7 \"Ta /,\ki(SPSLCY)\" 0 \"\" \"貝がら（6種ランダム）\" 1
                    // msg = /gift vcast_free_shell NULL \"名無し\" 0 \"\" \"貝がら（6種ランダム）\"
                    // 

                    // 「"」でメッセージを分割 (ニコニコのニックネームには「"」「'」が使えない)
                    string[] giftCmnt = msg.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);

                    // ユーザー名
                    string user = giftCmnt[1];

                    // 最後の文字「\」を削除
                    user = user.TrimEnd('\\');

                    // 特殊文字変換
                    user = user.Replace("\n", "").Replace("\r", "");
                    user = user.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
                    user = user.Replace("$", "＄").Replace("/", "／").Replace(",", "，");


                    // ポイント
                    string pt = giftCmnt[2];

                    // 最後の文字「\」を削除後、前後の空白を削除
                    pt = pt.TrimEnd('\\');
                    pt = pt.Trim();


                    // ギフト名
                    string giftName = giftCmnt[5];

                    // 最後の文字「\」を削除
                    giftName = giftName.TrimEnd('\\');

                    // 特殊文字変換
                    giftName = giftName.Replace("\n", "").Replace("\r", "");
                    giftName = giftName.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
                    giftName = giftName.Replace("$", "＄").Replace("/", "／").Replace(",", "，");


                    // ランキング(Vギフト)
                    string rank = "";
                    if (giftCmnt.Length == 7)
                    {
                        // 位取得後、前後の空白を削除
                        rank = giftCmnt[6];
                        rank = rank.Trim();
                    }
 
                    if (rank == "")
                    {
                        // Takiさんがギフト「お茶（300pt）」を贈りました
                        // 名無しさんがギフト「お茶（300pt）」を贈りました
                        msg =  user + "さんがギフト「" + giftName + "（" + pt + "pt）」を贈りました";
                    }
                    else
                    {
                        //【ギフト貢献1位】Takiさんがギフト「お茶（300pt）」を贈りました
                        msg = "【ギフト貢献" + rank + "位】" + user + "さんがギフト「" + giftName + "（" + pt + "pt）」を贈りました";
                    }
                    // 各要素処理しているから msg.Replace() は不要
                    break;
                case "/spi":
                    cmntSrc = "NicoliveSpi";
                    // 
                    // /spi \"「みんなでつりっくま」がリクエストされました\"
                    // ※名前の中に半角スペースあり
                    // /spi \"「ミノダ ひらがな 大 ピンク ゆ P50I9255」がリクエストされました\"
                    // /spi \"「Line Race」がリクエストされました\"
                    // 
                    msg = msg.Remove(0, 7); // 先頭7文字「/spi \"」削除
                    msg = msg.TrimEnd('\"');
                    msg = msg.TrimEnd('\\');
                    break;
                case "/emotion":
                    cmntSrc = "NicoliveEmotion";
                    // 
                    // /emotion 🌸
                    // /emotion 進捗どうですか？
                    // 「"」なし
                    //
                    msg = msg.Remove(0, 9); // 先頭9文字「/emotion 」削除
                    break;
                case "/cruise":
                    // 
                    // /cruise \"まもなく生放送クルーズが到着します\"
                    // /cruise \"生放送クルーズが去っていきます\"
                    // 
                    // msg = msg.Remove(0, 10); // 先頭10文字「/cruise \"」削除
                    msg = msg.TrimEnd('\"');
                    msg = msg.TrimEnd('\\');
                    break;
                case "/quote":
                    // 
                    // /quote \"「生放送クルーズさん」が引用を開始しました\"
                    // /quote \"ｗ（生放送クルーズさんの番組）\"
                    // /quote \"「生放送クルーズさん」が引用を終了しました\"
                    // 
                    // msg = msg.Remove(0, 9); // 先頭9文字「/quote \"」削除
                    msg = msg.TrimEnd('\"');
                    msg = msg.TrimEnd('\\');
                    break;
                case "/uadpoint":
                    // 
                    // /uadpoint 123456789 6400   // 123456789 放送ID lvなし
                    // 
                    msg = "広告が設定されました累計ポイントが" + str[2] + "になりました";
                    msg = msg.Replace("\\\"", "");
                    break;
                 case "/perm":
                    //
                    // /perm <a href="https://example.com/example/2020071700999" target="_blank"><u>●商品No.1 「サンプル S999」</u></a>
                    // ●商品No.1 「サンプル S999」
                    //
                    // <u><font color="#00CCFF"><a href="https://www.nicovideo.jp/watch/sm36179129" class="video" target="_blank">sm36179129</a></font></u> BGM「よいしょ（Yoisho）」
                    // sm36179129 BGM「よいしょ（Yoisho）」
                    //
                    if (str[1] == "<a") // タグ <a> と <u> を外す
                    {
                        msg = "／perm　＜リンク＞　　　　" + RemoveA(msg);
                    }
                    else if (str[1] == "<u><font") // タグ <u> と <font> と <a> を外す removeA とは別
                    {
                        msg = "／perm　＜リンク＞　　　　" + RemoveUFA(msg);
                    }
                    else
                    {
                        // do nothing
                    }
                    break;
                 case "/vote":
                    //
                    // /vote start 質問文 選択肢1 選択肢2 選択肢3 選択肢4 選択肢5 選択肢6 選択肢7 選択肢8 選択肢9
                    // /vote start 質問文 選択肢1 選択肢2 選択肢3
                    //
                    // 半角スペースが入ると「"」で囲まれる   （質問文・選択肢2）
                    // 半角スペースがないものはそのまま      （選択肢1） 
                    // 「"」があると「\"」に変換される       （選択肢3）
                    //
                    // /vote start \"質 問 文\" 選択肢1 \"選 択 肢 2\" 選\\"択肢3
                    //
                    // \\"を全角”に変換
                    // \"を検索　もしあったら次の \" を検索
                    // \" 間の半角をアンダースコアに置換
                    // 質問文 + 選択肢最大9 回繰り返す
                    // \"削除　半角スペースで分割して str 上書き
                    //
                    msg = msg.Replace("\\\\\"", "”");
                    // 結果
                    // /vote start \"質 問 文\" 選択肢1 \"選 択 肢 2\" 選”択肢3

                    // \"を検索　もしあったら次の \" を検索
                    // \" 間の半角をアンダースコアに置換
                    // 質問文 + 選択肢最大9 回繰り返す
                    //
                    int fmDQ = msg.IndexOf("\"");
                    if (fmDQ != -1)
                    {
                        int toDQ = msg.IndexOf("\"", fmDQ + 1);
                        msg = SpaceToUnderbar(msg, fmDQ, toDQ);

                        int fmDQ1 = msg.IndexOf("\"", toDQ + 1);
                        if (fmDQ1 != -1)
                        {
                            int toDQ1 = msg.IndexOf("\"", fmDQ1 + 1);
                            msg = SpaceToUnderbar(msg, fmDQ1, toDQ1);

                            int fmDQ2 = msg.IndexOf("\"", toDQ1 + 1);
                            if (fmDQ2 != -1)
                            {
                                int toDQ2 = msg.IndexOf("\"", fmDQ2 + 1);
                                msg = SpaceToUnderbar(msg, fmDQ2, toDQ2);

                                int fmDQ3 = msg.IndexOf("\"", toDQ2 + 1);
                                if (fmDQ3 != -1)
                                {
                                    int toDQ3 = msg.IndexOf("\"", fmDQ3 + 1);
                                    msg = SpaceToUnderbar(msg, fmDQ3, toDQ3);

                                    int fmDQ4 = msg.IndexOf("\"", toDQ3 + 1);
                                    if (fmDQ4 != -1)
                                    {
                                        int toDQ4 = msg.IndexOf("\"", fmDQ4 + 1);
                                        msg = SpaceToUnderbar(msg, fmDQ4, toDQ4);

                                        int fmDQ5 = msg.IndexOf("\"", toDQ4 + 1);
                                        if (fmDQ5 != -1)
                                        {
                                            int toDQ5 = msg.IndexOf("\"", fmDQ5 + 1);
                                            msg = SpaceToUnderbar(msg, fmDQ5, toDQ5);

                                            int fmDQ6 = msg.IndexOf("\"", toDQ5 + 1);
                                            if (fmDQ6 != -1)
                                            {
                                                int toDQ6 = msg.IndexOf("\"", fmDQ6 + 1);
                                                msg = SpaceToUnderbar(msg, fmDQ6, toDQ6);

                                                int fmDQ7 = msg.IndexOf("\"", toDQ6 + 1);
                                                if (fmDQ7 != -1)
                                                {
                                                    int toDQ7 = msg.IndexOf("\"", fmDQ7 + 1);
                                                    msg = SpaceToUnderbar(msg, fmDQ7, toDQ7);

                                                    int fmDQ8 = msg.IndexOf("\"", toDQ7 + 1);
                                                    if (fmDQ8 != -1)
                                                    {
                                                        int toDQ8 = msg.IndexOf("\"", fmDQ8 + 1);
                                                        msg = SpaceToUnderbar(msg, fmDQ8, toDQ8);

                                                        int fmDQ9 = msg.IndexOf("\"", toDQ8 + 1);
                                                        if (fmDQ9 != -1)
                                                        {
                                                            int toDQ9 = msg.IndexOf("\"", fmDQ9 + 1);
                                                            msg = SpaceToUnderbar(msg, fmDQ9, toDQ9);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // 結果
                    // /vote start \"質_問_文\" 選択肢1 \"選_択_肢_2\" 選”択肢3

                    msg = msg.Replace("\\\"", "");
                    // 結果
                    // /vote start 質_問_文 選択肢1 選_択_肢_2 選”択肢3

                    // str 上書き
                    str = msg.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // /vote start 質_問_文 選択肢1 選_択_肢_2 選”択肢3
                    // 【アンケート開始】質_問_文
                    // 1：選択肢1
                    // 2：選_択_肢_2
                    // 3：選”択肢3
                    //
                    // 改行文字数調整不可能ではないが複雑すぎるので１行にする
                    // 【アンケート開始】質_問_文　1：選択肢1　2：選_択_肢_2　3：選”択肢3
                    //
                    // /vote showresult per 833 167 0
                    // 【アンケート結果】
                    // 1：83.3%　2：16.7%　3：0%
                    //
                    // /vote stop
                    // 【アンケート終了】
                    //
                    if (str[1] == "start")
                    {
                        msg = "【アンケート開始】" + str[2];

                        // 最大9つの選択肢の結果 
                        int len = str.Length;
                        for (int i = 3; i < len; i++)
                        {
                            if (str[i] != String.Empty)
                            {
                                int num = i - 2;
                                string orderNum = num.ToString();
                                msg = msg + "　" + orderNum + "：" + str[i];
                                // 結果
                                // msg = "【アンケート開始】質_問_文　1：選択肢1　2：選_択_肢_2　3：選”択肢3";
                            }
                            else
                            {
                                // do nothing str[i] = Empty
                            }
                        }
                    }
                    else if (str[1] == "showresult")
                    {
                        msg = "【アンケート結果】　　　　　"; // 改行の関係からここは14字
                        // 最大9つの選択肢の結果 
                        int len = str.Length;
                        for (int i = 3; i < len; i++)
                        {
                            if (str[i] != String.Empty)
                            {
                                int striLen = str[i].Length;
                                if (striLen == 1)
                                {
                                    str[i] = "0." + str[i]; // 6 -> 0.6
                                    int dotZero = str[i].IndexOf(".0");
                                    if (dotZero != -1)
                                    {
                                        str[i] = str[i].Remove(dotZero); // 0.0 -> 0
                                    }
                                }
                                else if (striLen == 2)
                                {
                                    str[i] = str[i].Insert(1, "."); // 83 -> 8.3
                                    // NCVでは「.0」が省略されるが画面上は表示される
                                    //int dotZero = str[i].IndexOf(".0");
                                    //if (dotZero != -1)
                                    //{
                                    //    str[i] = str[i].Remove(dotZero); // 5.0 -> 5
                                    //}
                                }
                                else if (striLen == 3)
                                {
                                    str[i] = str[i].Insert(2, "."); // 917 -> 91.7
                                    // NCVでは「.0」が省略されるが画面上は表示される
                                    //int dotZero = str[i].IndexOf(".0");
                                    //if (dotZero != -1)
                                    //{
                                    //    str[i] = str[i].Remove(dotZero); // 75.0 -> 75
                                    //}
                                }
                                else
                                {
                                    str[i] = "100"; // 1000 -> 100
                                }
                                int num = i - 2;
                                string orderNum = num.ToString();
                                msg = msg + "　" + orderNum + "：" + str[i] + "%";
                                // 結果
                                // msg = "【アンケート結果】　　　　　　1：91.7%　2：8.3%　3：0%";
                                // msg = "【アンケート結果】　　　　　　1：75.0%　2：20.0%　3：5.0%";
                            }
                            else
                            {
                                // do nothing str[i] = Empty
                            }
                        }
                    }
                    else if (str[1] == "stop")
                    {
                        msg = "【アンケート終了】";
                    }
                    else
                    {
                        // do nothing
                    }
                    break;
                case "<a":
                    //
                    // <a href="https://example.jp/example/example.html?from=live_watch_anime202099_player" target="_blank"><u>今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ</u></a>
                    // 今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ
                    //
                    // タグ <a> と <u> を外す
                    msg = "＜リンク＞" + RemoveA(msg);
                    break;
                case "<u><font":
                    //
                    // <u><font color="#00CCFF"><a href="https://www.nicovideo.jp/watch/sm36179129" class="video" target="_blank">sm36179129</a></font></u> てすと
                    // sm36179129 BGM「よいしょ（Yoisho）」
                    //
                    // タグ <u> と <font> と <a> を外す removeA とは別
                    msg = "＜リンク＞" + RemoveUFA(msg);
                    break;
                // 
                // デフォルト処理でいい
                // case "/disconnect":
                //    msg = "disconnect";
                //    msg = msg.Replace("\\\"", "");
                //    break;
                // 
                default:
                    // msg = msg.Replace("\n", "").Replace("\r", "").Replace("$", "＄").Replace("/", "／").Replace(",", "，");
                    // msg = msg.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
                    break;
            }

            // 念のため
            if (msg == null)
            {
                msg = "（本文なし）";
            }

            // 改行コード等々
            msg = msg.Replace("\n", "").Replace("\r", "");
            msg = msg.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
            msg = msg.Replace("$", "＄").Replace("/", "／").Replace(",", "，");

            // 編集した運営コメントを 返す
            return (msg, cmntSrc);
        }
        
        /// <summary>
        /// タグ <a> と <u> を外す
        /// </summary>        
        static string RemoveA(string msg)
        {
            //
            // /perm <a href="https://example.com/example/2020071700999" target="_blank"><u>●商品No.1 「サンプル S999」</u></a>
            // ●商品No.1 「サンプル S999」
            // 
            // <a href="https://example.jp/example/example.html?from=live_watch_anime202099_player" target="_blank"><u>今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ</u></a>
            // 今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ
            //
            // 先頭から <u> を検索 + 3 まで削除
            // 後ろに文字列がつく可能性あり　単に </u> </a> 削除
            //
            string linkName;
            int fmLinkName = msg.IndexOf("<u>");
            if (fmLinkName == -1) // <u> がなかったら
            {
                int fmLinkName1 = msg.IndexOf("_blank\\\">");
                if (fmLinkName1 == -1)
                {
                    // エラーが出るよりまし
                    linkName = msg;
                }
                else
                {
                    fmLinkName = fmLinkName1 + 9;
                    linkName = msg.Remove(0, fmLinkName);
                }
            }
            else
            {
                fmLinkName = fmLinkName + 3;
                linkName = msg.Remove(0, fmLinkName);
                // 結果
                // linkName = ●商品No.1 「サンプル S999」</u></a>
                // linkName = 今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ</u></a>
            }

            linkName = linkName.Replace("</a>", "").Replace("</font>", "").Replace("</u>", "");
            linkName = linkName.Replace("\n", "").Replace("\r", "");
            linkName = linkName.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
            linkName = linkName.Replace("$", "＄").Replace("/", "／").Replace(",", "，");
            // 結果
            // linkName = ●商品No.1 「サンプル S999」
            // linkName = 今期アニメ　niconicoでの配信一覧はこちら！｜Nアニメ

            msg = linkName;
            return msg;
        }

        /// <summary>
        /// タグ <u> と <font> と <a> を外す
        /// </summary>        
        static string RemoveUFA(string msg)
        {
            //
            // /perm <u><font color=\"#00CCFF\"><a href=\"https://www.nicovideo.jp/watch/sm36179129\" class=\"video\" target=\"_blank\">sm36179129</a></font></u>
            // sm36179129
            // 
            // <u><font color=\"#00CCFF\"><a href=\"https://www.nicovideo.jp/watch/sm36179129\" class=\"video\" target=\"_blank\">sm36179129</a></font></u>
            // sm36179129
            //
            // <u><font color="#00CCFF"><a href="https://www.nicovideo.jp/watch/sm36179129" class="video" target="_blank">sm36179129</a></font></u> BGM「よいしょ（Yoisho）」
            // sm36179129 BGM「よいしょ（Yoisho）」
            //
            // lvから始まる放送番号も同じ形式
            //
            // 先頭から <_blank> を検索 + 8 まで削除
            // 後ろに文字列がつく可能性あり　単に </a> </font> </u> 削除
            //
            string linkName;
            int fmLinkName = msg.IndexOf("_blank\\\">");
            if (fmLinkName == -1) // _blank"> がなかったら
            {
                // エラーが出るよりまし
                linkName = msg;
            }
            else
            {
                fmLinkName = fmLinkName + 9;
                linkName = msg.Remove(0, fmLinkName);
                // 結果
                // linkName = sm36179129</a></font></u> BGM「よいしょ（Yoisho）」
            }

            linkName = linkName.Replace("</a>", "").Replace("</font>", "").Replace("</u>", "");
            linkName = linkName.Replace("\n", "").Replace("\r", "");
            linkName = linkName.Replace("\\\"", "”").Replace("\\\'", "’").Replace("\\", "＼");
            linkName = linkName.Replace("$", "＄").Replace("/", "／").Replace(",", "，");
            // 結果
            // linkName = sm36179129 BGM「よいしょ（Yoisho）」

            msg = linkName;
            return msg;
        }

        /// <summary>
        /// 範囲内で半角スペースを半角アンダースコアに置換
        /// </summary>        
        static string SpaceToUnderbar(string msg, int from, int to)
        {
            // StringBuilderを作成する
            StringBuilder sb = new StringBuilder(msg);
            // from から to の範囲で、半角スペースを半角アンダースコアに置換する
            sb.Replace(" ", "_", from, to - from);
            // msgに戻す
            msg = sb.ToString();
            return msg;
        }
        #endregion
    }
}