# emoji-cache-viewer

[yt-dlp-GUI](https://github.com/5cd8/yt-dlp-GUI) が作る `emoji_cache.sqlite` の中身を、画像の一覧として確認する検証用ツール（WinForms、`net8.0-windows`）。yt-dlp-GUI の絵文字キャッシュ投入（キュー項目・フォルダ一括投入・チャンネル事前投入）で絵文字が入ったかを目で確かめるために使う。

## 使い方

```bash
dotnet run --project EmojiCacheViewer.csproj -- <emoji_cache.sqlite またはそのフォルダ>
```

引数なしで起動して「開く…」から選んでもよい。

- 全件を48pxのサムネイルとURL末尾のラベルで表示する。URLの部分一致で絞り込める。
- 項目を選ぶと、右側に原寸の画像・URL・バイト数を表示する。
- SQLite は読み取り専用（`Mode=ReadOnly`）で開く。キャッシュは書き換えない。
- 表示に使う GDI+ は WebP を読めない。読めない BLOB は「?」で表示し、件数をヘッダーに出す。

## テーブル

`emoji_cache(url TEXT PRIMARY KEY, data BLOB NOT NULL)`（yt-dlp-GUI・vlc-chat と共有する契約）。
