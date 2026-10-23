/* ============================================================================
 * banlan ECharts 主题 — DRAFT (L2 第一刀)
 * ----------------------------------------------------------------------------
 * 治理:rfc/banlan-chart-spec-proposal.md(§2 色板 / §3 几何)· PLAN.md §3.5(已拍板)
 * 状态:ACCEPTED —— PLAN §3.5(chenxu 拍板 2026-06-14)。系列色绑 --chart-series-*
 *       语义层(styles.css :root,真源 foundations/chart-color-foundation)。
 *       L2(把本主题封进 <bl-chart> runtime tag)仍 deferred,见 RFC §5。
 *
 * 设计要点:
 *  - 运行时读 banlan CSS 自定义属性(getComputedStyle),不硬编 hex;
 *    fallback hex 仅在 banlan styles.css 未加载时兜底,且 = 该 token 的文档值。
 *  - 深色(L1 验证实测):banlan 全局只 dark-remap 少数 token(drop-shadow + 6 status);
 *    --component-color-white / --text-color-* / grayB 等结构 token 的 dark 值是
 *    per-component scoped、不在 :root,故经全局 token 读取在深色下仍拿浅色值。
 *    图表 dark 适配是 follow-up(需 banlan 补全局 dark 图表 token 或主题显式 dark 分支)。见 RFC §6。
 *  - 系列色绑 --chart-series-1..6 语义层(PLAN §3.5 已拍板引入);
 *    该层在 styles.css :root 别名既有 token(brand/blue/cyan/orange/red/grayB),
 *    fallback hex = 各 token 文档值,兜底 styles.css 未加载场景。
 * ========================================================================== */
(function (global) {
  function cssVar(name, fallback) {
    try {
      if (typeof document === 'undefined') return fallback;
      var v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return v || fallback;
    } catch (e) { return fallback; }
  }

  function buildBanlanEchartsTheme() {
    // 6 系列色 —— 绑 --chart-series-N 语义层(PLAN §3.5 / RFC §2.B);
    // fallback hex = 该槽位映射的既有 token 文档值(RFC §2.A)
    var series = [
      cssVar('--chart-series-1', '#5c61ff'), // 0 主强调   (→ brand-color-5)
      cssVar('--chart-series-2', '#30b5f2'), // 1 次级     (→ blue-color-5)
      cssVar('--chart-series-3', '#29ccc9'), // 2 第三     (→ cyan-color-5)
      cssVar('--chart-series-4', '#ff7e0d'), // 3 警告     (→ orange-color-6, warning 基色)
      cssVar('--chart-series-5', '#fa423c'), // 4 控制/错误 (→ red-color-6, error 基色)
      cssVar('--chart-series-6', '#c5cbd6')  // 5 中性/其他 (→ grayB-color-4)
    ];
    // 外壳 token(注:banlan 全局未 dark-remap 这些结构 token,深色不自动跟,见 RFC §6)
    var textPrimary  = cssVar('--text-color-primary',  '#000000');
    var textTertiary = cssVar('--text-color-tertiary', '#818999');
    var lineLight    = cssVar('--grayB-color-2',       '#f2f5fa'); // splitLine
    var axisLine     = cssVar('--grayB-color-3',       '#e4e8f0');
    var cardBg       = cssVar('--component-color-white','#ffffff'); // tooltip bg
    var fontFamily   = 'PingFang SC, -apple-system, "Helvetica Neue", Arial, sans-serif';

    return {
      color: series,
      backgroundColor: 'transparent',
      textStyle: { fontFamily: fontFamily, color: textTertiary },
      title: { textStyle: { color: textPrimary, fontWeight: 500, fontSize: 14, fontFamily: fontFamily } },
      // 图例:圆点 6px(对齐 bl-tag__dot)+ 12px 三级文本
      legend: {
        icon: 'circle', itemWidth: 6, itemHeight: 6, itemGap: 16,
        textStyle: { color: textTertiary, fontSize: 12, fontFamily: fontFamily }
      },
      // Tooltip:对齐 bl-popover(白底/描边/8 圆角/气泡阴影),绑 token 自动适配深色
      tooltip: {
        backgroundColor: cardBg, borderColor: axisLine, borderWidth: 1,
        textStyle: { color: textPrimary, fontSize: 12, fontFamily: fontFamily },
        extraCssText: 'border-radius:8px;box-shadow:0 2px 10px rgba(0,25,126,0.08);padding:8px 12px;'
      },
      categoryAxis: {
        axisLine: { lineStyle: { color: axisLine } },
        axisTick: { show: false },
        axisLabel: { color: textTertiary, fontSize: 10, fontFamily: fontFamily },
        splitLine: { show: false }
      },
      valueAxis: {
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: textTertiary, fontSize: 10, fontFamily: fontFamily },
        splitLine: { lineStyle: { color: lineLight, type: 'dashed' } }
      },
      // 柱:顶部圆角 2px(RFC §3.B 产品规范明示),最大宽 16px
      bar: { itemStyle: { borderRadius: [2, 2, 0, 0] }, barMaxWidth: 16 },
      // 线:1px(对齐 chart-section L150 默认)
      line: { lineStyle: { width: 1 }, symbol: 'circle', symbolSize: 4 },
      // 饼/环:扇区描边用卡片底色拉开间隙
      pie: { itemStyle: { borderColor: cardBg, borderWidth: 2 }, label: { color: textTertiary, fontFamily: fontFamily } }
    };
  }

  global.buildBanlanEchartsTheme = buildBanlanEchartsTheme;
  global.registerBanlanEchartsTheme = function (echarts, name) {
    echarts.registerTheme(name || 'banlan', buildBanlanEchartsTheme());
    return name || 'banlan';
  };
})(typeof window !== 'undefined' ? window : this);
