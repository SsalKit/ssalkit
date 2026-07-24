[English](README.md) | [한국어](README.ko.md) | **日本語**

# SsalKit

モダンな .NET のための、ソースジェネレーター中心のユーティリティライブラリ集です。
[![CI](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml/badge.svg)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![coverage](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fssalkit%2Fssalkit%2Fbadges%2Fcoverage.json)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Libraries

| パッケージ | NuGet | 説明 | ドキュメント |
|---------|-------|-------------|------|
| `SsalKit.DependencyInjection` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.DependencyInjection) | Roslyn ソースジェネレーターによるコンパイル時 DI 自動登録ライブラリ。 | [README](src/SsalKit.DependencyInjection/README.ja.md) |
| `SsalKit.Randomness` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness) | 決定的・状態シリアライズ可能な PRNG（xoshiro256\*\* + SplitMix64）と重み付きランダム抽選ライブラリ。 | [README](src/SsalKit.Randomness/README.ja.md) |

## 名前の由来

SsalKit の「Ssal」は、韓国語で米（쌀）のことです。ゲームで稼いだ財貨で現実の米を買って食べることを指す韓国のミーム「サルモク（쌀먹）」にちなんでいます。開発に使うライブラリからも、同じように毎日の「ご飯代」になるくらい実用的な価値をしっかり搾り取っていこう——そんな思いを込めた名前です。

## 設計思想

- **コンパイル時ファースト。** 動作はランタイムで発見されるのではなく、ビルド時に通常の C# コードとして生成されます。
- **リフレクションゼロ。** 生成されるコードは、ごく普通の読みやすくデバッグ可能なコードです。動的に探索されるものはありません。
- **AOT・トリミングに親和的。** リフレクションによる探索がないため、追加の注釈なしにトリミングと Native AOT をそのまま通過します。
- **コンパイル時診断。** 誤った使い方は、本番環境での実行時例外ではなく、コンパイラの段階で検出されます。

## 動作要件

.NET 10+

## ライセンス

MIT — 詳細は [LICENSE](LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
