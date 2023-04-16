# nBatonOSC: NCV で受け取ったコメントや関連情報を OSC で転送する プラグイン
このプラグインを使うと、 NCV (Niconama Comment Viewer) で受け取ったコメントや関連情報を VirtualCast に OSC (Open Sound Control) で送信することができるようになります。  

## 必要環境
1. NCV version α216 以上を推奨  
   ユーザー名取得の関係から α216 以上を推奨します。  
   2023年4月16日現在、 α216 は「過去のバージョン・テスト版」の方に上げられています。  
   NCV の取得は[こちら (posite-c)](https://www.posite-c.com/) から  

## インストール
インストール手順は次の通り  
1. 「nBatonOSC.dll」をダウンロード(または自分でコンパイル)  
2. 「nBatonOSC.dll」を右クリックし、プロパティのセキュリティ項目「許可する」にチェック  
   右クリック→プロパティ→セキュリティ:このファイルは…☑許可する(K)  
3. 「nBatonOSC.dll」を NCV の plugins フォルダに置く  
   例 C:\Program Files (x86)\posite-c\NiconamaCommentViewer\plugins  
4. NCV を立ち上げる  
   NCV を立ち上げると設定ファイル「nBatonOSC.txt」が NCV の設定フォルダに自動作成されます。  
   設定ファイルに関して、基本的にユーザー側での作業はありません。  
   (参考: 既定のフォルダ「C:\Users\%ユーザー名%\AppData\Roaming\posite-c\NiconamaCommentViewer」は Windows の初期設定では隠しフォルダになっています。NCV のインストール先を変えた場合は違うフォルダかもしれません。「UserSetting.xml」と同じフォルダに自動作成されます。)  

# アンインストール
アンインストール手順は次の通り  
1. 「nBatonOSC.dll」を削除  
   設定ファイル「nBatonOSC.txt」は上記隠しフォルダ内にあります。削除できる方は手動で削除してください。  
   (参考: 設定ファイルは転送モードの数字「0」「1」「2」のどれか１文字が書いてあるテキストファイルです。)  

## 使用方法
使用方法は次の通り  
1. NCV のメニュー「プラグイン」から「nBatonOSC v-- 設定 (Settings)」を選択  
2. 転送モードを選択し、設定画面を閉じる (設定画面を閉じると新しい設定が反映されます)  

## VirtualCastとの連携
VCI と連携させる手順は次の通り  
1. VirtualCast タイトル画面の「VCI」の中にある「OSC受信機能」を「creator-only」または「enabled」に設定 (どちらを選んだらよいかよくわからなかったら「creator-only」を選択して様子を見てみてください。)  
   (参考: このプラグインでは VirtualCast の「OSC送信機能」は利用していません。)  
2. 「コメントバトン (OSC) Comment baton」と「縦書きコメビュ VCV」を [VirtualCastのページ](https://virtualcast.jp/users/100215#products) から入手  

## プラグインの挙動
OSC 送信形式は2種類  
1. コメント関連情報の送信  
   コメントを受信した際、転送モードが「1:スタジオ[運営コメント]」または「2:ルーム[全転送]」の時に、下記形式で送信  

   UDPSender("127.0.0.1", 19100);  
   OscMessage("/vci/baton/comment", `blob_comment`, `blob_name`, `str_commentSource`, `int_transferMode`); 

2. 転送モードの送信  
   NCV の起動時、終了時、放送接続時、転送モード変更時に下記形式で送信  

   UDPSender("127.0.0.1", 19100);  
   OscMessage("/vci/baton/mode", `int_transferMode`);  

## 引数の型及び具体例
ここでは OSC メッセージで使われる引数の型と具体例を紹介します。  
1. "/vci/baton/comment", "/vci/baton/mode"  
   OSC メッセージの送信先や目的を識別するための OSC アドレス  

2. `blob_comment`  
   コメント (BlobAsUtf8 UTF-8 文字列を表すバイト列)  
   - Twitterの画像連携等でコメントが空白の時は「（本文なし）」  
   - コメントソースが NicoliveAd, NicoliveInfo, NicoliveGift, NicoliveSpi, NicoliveEmotion の時は、「/nicoad」「/info *」「/gift」「/spi」「/emotion」を削除する等、編集  
   - その他、制御文字の全角変換等の編集

3. `blob_name`  
   ユーザー名 (BlobAsUtf8 UTF-8 文字列を表すバイト列)  
   - 運営コメントの場合は「（運営）」  
   - 生ID及び 184 で、固定ハンドルネーム(以下コテハン)が登録されている場合はそのコテハン  
   - コテハンが登録されていない生 ID の場合、ユーザー名が取得できればユーザー名、取得できなければ「（生ID）」  
   - コテハンが登録されていない 184 の場合、184 の ID  

4. `str_commentSource`  
   コメントソース (String ASCII 文字列)  
   - 一般コメント: Nicolive  
   - 184 コメント: Nicolive184  
   - 運営コメント(既定値): NicoliveOperator  
   - 特定の運営コメント: NicoliveAd, NicoliveInfo, NicoliveGift, NicoliveSpi, NicoliveEmotion  

5. `int_transferMode`  
   転送モード (Int32 32bit 整数)  
   - NCV 起動時は設定ファイルに保存されているモードの数字(「0」「1」「2」 のどれか)  
   - 転送モード変更時はその新モードの数字  
    0: 転送しない  
    1: スタジオ[運営コメント]  
    2: ルーム[全転送]  
   - NCV 終了時には「-1」を送信  

## VCI 側 main.lua の例
コメントの受信  
```lua:main.lua
function exampleComment(comment, senderName, senderCommentSource, mode)
  local isOperator = ((senderCommentSource == 'NicoliveOperator')
                   or (senderCommentSource == 'NicoliveAd') 
                   or (senderCommentSource == 'NicoliveInfo')
                   or (senderCommentSource == 'NicoliveGift') 
                   or (senderCommentSource == 'NicoliveSpi')
                   or (senderCommentSource == 'NicoliveEmotion'))
  if isOperator then
    print('運営コメント')
  end  
end  

-- OSC: コメント受信  
vci.osc.RegisterMethod('/vci/baton/comment', exampleComment, {vci.osc.types.BlobAsUtf8, vci.osc.types.BlobAsUtf8, vci.osc.types.String, vci.osc.types.Int32})  
```
これは VCI 側でコメントを受信するスクリプトの例です。関数 exampleComment は OSC で受信した 4 つの引数 (コメント、名前、コメントソース、転送モード) を受け取ります。コメントソースが「NicoliveOperator」や「NicoliveAd」等の時、デバッグコンソールに「運営コメント」と表示します。

転送モードの受信  
```lua:main.lua
function exampleMode(mode)
  if (mode == 0) then
    print("転送しない")
  end  
end  

-- OSC: 転送モード受信  
vci.osc.RegisterMethod('/vci/baton/mode', exampleMode, {vci.osc.types.Int32})  
```
これは VCI 側で転送モードを受信するスクリプトの例です。関数 exampleMode は OSC で受信した 1 つの引数 (転送モード) を受け取ります。転送モードが「0」の時、デバッグコンソールに「転送しない」と表示します。  

詳しくは [バーチャルキャスト公式Wiki: ExportOsc(外部との OSC 通信)](https://wiki.virtualcast.jp/wiki/vci/script/reference/exportosc) をご覧ください。  

## ライセンス
このプラグインは MIT ライセンスです。  

# nBatonOSC: NCV plugin for transferring comments and related information by OSC.
This is a plugin that allows you to send comments and related information received from NCV (Niconama Comment Viewer) to VirtualcCast using OSC (Open Sound Control) protocol.

## Requirements
1. NCV version α216 or higher  
   α216 or higher is recommended to get better information about username.  
   As of April 16, 2023, α216 is still listed in the "Past version, Test version".  
   You can get NCV from [here (posite-c)](https://www.posite-c.com/)

## Installation
To install this plugin, please follow these steps:  
1. Download "nBatonOSC.dll" (or compile it by yourself). 
2. Right-click on "nBatonOSC.dll", select "Properties", and tick the checkbox named "Unblock" in Security.   
   Right click -> Properties -> "☑Unblock".  
3. Put "nBatonOSC.dll" in the NCV folder.  
   e.g. C:\Program Files (x86)\posite-c\NiconamaCommentViewer\plugins  
4. Boot NCV.  
   After booting NCV, the setting file "nBatonOSC.txt" will be created automatically.  
   You don't have to change anything in the setting file.  
   (FYI, the default folder "C:\Users\%UserName%\AppData\Roaming\posite-c\NiconamaCommentViewer" is hidden by Windows' default settings. Besides, if you installed NCV in custom folder, the setting folder might be different. The folder is the same folder that has "UserSetting.xml".)  

## Uninstallation
To uninstall this plugin, please follow these steps:
1. Delete "nBatonOSC.dll".  
   The setting file "nBatonOSC.txt" is in the above hidden folder. If you can find it, please delete it as well.  
   (FYI, the setting file "nBatonOSC.txt" is just a text file that has only a letter "0" or "1" or "2".)  

## Usage
To use this plugin, please follow these steps:
1. Select "nBatonOSC v-- -- (Settings)" from the NCV "plugin" menu.  
2. Select the transfer mode and then close the setting window (when the settings window is closed, the new transfer mode is applied).  

## Linking to VirtualCast
To link to the VCI in Virtualcast, please follow these steps:
1. Select "creator-only" or "enabled" from the "OSC Receive Function" drop-down menu in the "VCI" option of the VirtualCast title-window, (if you don't know which one is better for you, select “creator-only” and see how it goes).  
   (FYI, this plugin does not use the function of VirtualCast's "OSC Send".)
2. Get VCI "コメントバトン (OSC) Comment baton" and "縦書きコメビュ VCV" from [VirtualCast](https://virtualcast.jp/users/100215#products).  

## This plugin's behavior
There are two types of OSC sending.

1. Sending comments and related information  
   When NCV receives a comment, if the transfer mode is "1: Studio [Operator comments]" or "2: Room [All comments]", then this plugin sends the comment in the following format.

   UDPSender("127.0.0.1", 19100);  
   OscMessage("/vci/baton/comment", `blob_comment`, `blob_name`, `str_commentSource`, `int_transferMode`); 
  
2. Sending the transfer mode  
   When NCV is booted or closed, when connecting to niconico Live, when the transfer mode is changed, this plugin sends the transfer mode in the following format.

   UDPSender("127.0.0.1", 19100);  
   OscMessage("/vci/baton/mode", `int_transferMode`);  

## Argument type and examples
This section explains the types and examples of the arguments that are used in the OSC messages.
1. "/vci/baton/comment", "/vci/baton/mode"  
   These are the OSC addresses. An OSC address is a string that identifies the destination or the purpose of an OSC message.  

2. `blob_comment`  
   This is a comment (BlobAsUtf8 UTF-8 Blob type). Some examples are:
   - For example, if the comment was "", such as when transferring an image from Twitter, this plugin adds the letters "（本文なし）" (it means "(no content)" in Japanese).  
   - For instance, if the comment source was NicoliveAd, NicoliveInfo, NicoliveGift, NicoliveSpi, or NicoliveEmotion, the comment would be edited, such as deleting "/nicoad", "/info *", "/gift", "/spi", "/emotion".  
   - Also, full-width conversion of control characters, etc.
  
3. `blob_name`  
   This is a username (BlobAsUtf8 UTF-8 Blob type). Some examples are:
   - If the comment was made by an operator, it would be "（運営）" (it means "(Operator)" in Japanese).
   - If NCV has a nickname for the raw ID or the anonymous ID, it would be the nickname.
   - If NCV does not have a nickname for the raw ID and this plugin can get the niconico username, it would be the niconico username. Otherwise, if this plugin can not get the username, it would be "（生ID）" (it means "(Raw ID)" in Japanese) as the username.
   - If NCV does not have a nickname for the anonymous user, it would be the anonymous ID.
  
4. `str_commentSource`  
   This is a comment source (String ASCII Letters). Some examples are:
   - User comment: Nicolive
   - Anonymous user comment: Nicolive184
   - Operator comment (default): NicoliveOperator
   - Some specific operator comment: NicoliveAd, NicoliveInfo, NicoliveGift, NicoliveSpi, NicoliveEmotion

5. `int_transferMode`  
   This is a Transfer mode (Int32 32bit Integer). Some examples are:
   - When NCV was booted, this plugin reads the transfer mode number ("0" or "1" or "2") from the setting file.  
   - When the transfer mode is changed, it would be the new mode number.  
    0: Off  
    1: Studio [Operator comments]  
    2: Room [All comments]  
   - When NCV is closed, this plugin sends "-1".  

## VCI side: example for the main.lua
Receive comment  
```lua:main.lua
function exampleComment(comment, senderName, senderCommentSource, mode)
  local isOperator = ((senderCommentSource == 'NicoliveOperator')
                   or (senderCommentSource == 'NicoliveAd') 
                   or (senderCommentSource == 'NicoliveInfo')
                   or (senderCommentSource == 'NicoliveGift') 
                   or (senderCommentSource == 'NicoliveSpi')
                   or (senderCommentSource == 'NicoliveEmotion'))
  if isOperator then
    print('Operator comment')
  end  
end  

-- OSC: receive comment  
vci.osc.RegisterMethod('/vci/baton/comment', exampleComment, {vci.osc.types.BlobAsUtf8, vci.osc.types.BlobAsUtf8, vci.osc.types.String, vci.osc.types.Int32})  
```
This is an example of a script that receives comments from the VCI. The function exampleComment takes four arguments (comment, name, comment source, transfer mode) that are received by OSC. When the comment source is "NicoliveOperator", "NicoliveAd", etc., it displays "Operator comment" on the debug console.

Receive transfer mode  
```lua:main.lua
function exampleMode(mode)
  if (mode == 0) then
    print("Off")
  end  
end  

-- OSC: receive transfer mode  
vci.osc.RegisterMethod('/vci/baton/mode', exampleMode, {vci.osc.types.Int32})  
```
This is an example of a script that receives the transfer mode from VCI. The function exampleMode takes one argument (transfer mode) that is received by OSC. When the transfer mode is "0", it displays "Do not transfer" on the debug console.  

For more details, please see [VirtualCast Official Wiki: ExportOsc(外部との OSC 通信)](https://wiki.virtualcast.jp/wiki/vci/script/reference/exportosc)  

## License
This plugin is licensed under the MIT License.
