namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 公司名称模糊搜索 —— 把"茅台"映射到"600519"
/// 支持中文名、股票代码、简称、行业的模糊匹配
/// 优先查本地库（~230 家），未命中时回退到东方财富
/// </summary>
public class CompanySearchService : ICompanySearchService
{
    private readonly IEastMoneyService? _eastMoney;
    private readonly ILogger<CompanySearchService>? _logger;

    public CompanySearchService(IEastMoneyService? eastMoney = null, ILogger<CompanySearchService>? logger = null)
    {
        _eastMoney = eastMoney;
        _logger = logger;
    }

    private static readonly Dictionary<string, CompanyInfo> _companies = new()
    {
        // ===== 金融 - 银行 =====
        ["600036"] = new("600036", "招商银行", "zhaohang", "银行"),
        ["601398"] = new("601398", "工商银行", "gongshang", "银行"),
        ["601939"] = new("601939", "建设银行", "jianhang", "银行"),
        ["601288"] = new("601288", "农业银行", "nonghang", "银行"),
        ["601988"] = new("601988", "中国银行", "zhonghang", "银行"),
        ["601328"] = new("601328", "交通银行", "jiaohang", "银行"),
        ["601658"] = new("601658", "邮储银行", "youchu", "银行"),
        ["600000"] = new("600000", "浦发银行", "pufa", "银行"),
        ["601166"] = new("601166", "兴业银行", "xingye", "银行"),
        ["600016"] = new("600016", "民生银行", "minsheng", "银行"),
        ["601998"] = new("601998", "中信银行", "zhongxin", "银行"),
        ["601818"] = new("601818", "光大银行", "guangda", "银行"),
        ["000001"] = new("000001", "平安银行", "pingan", "银行"),
        ["002142"] = new("002142", "宁波银行", "ningbo", "银行"),
        ["601229"] = new("601229", "上海银行", "shanghai", "银行"),
        ["601169"] = new("601169", "北京银行", "beijing", "银行"),
        ["600015"] = new("600015", "华夏银行", "huaxia", "银行"),
        ["601009"] = new("601009", "南京银行", "nanjing", "银行"),
        ["600919"] = new("600919", "江苏银行", "jiangsu", "银行"),
        ["600926"] = new("600926", "杭州银行", "hangzhou", "银行"),
        ["601577"] = new("601577", "长沙银行", "changsha", "银行"),
        ["601838"] = new("601838", "成都银行", "chengdu", "银行"),
        ["601963"] = new("601963", "重庆银行", "chongqing", "银行"),
        ["601997"] = new("601997", "贵阳银行", "guiyang", "银行"),
        ["002948"] = new("002948", "青岛银行", "qingdao", "银行"),
        ["002966"] = new("002966", "苏州银行", "suzhou", "银行"),

        // ===== 金融 - 证券 =====
        ["600030"] = new("600030", "中信证券", "zhongxin", "券商"),
        ["601688"] = new("601688", "华泰证券", "huatai", "券商"),
        ["600837"] = new("600837", "海通证券", "haitong", "券商"),
        ["601211"] = new("601211", "国泰君安", "guotai", "券商"),
        ["000776"] = new("000776", "广发证券", "guangfa", "券商"),
        ["600999"] = new("600999", "招商证券", "zhaoshang", "券商"),
        ["601066"] = new("601066", "中信建投", "jianye", "券商"),
        ["000166"] = new("000166", "申万宏源", "shenwan", "券商"),
        ["601881"] = new("601881", "中国银河", "yinhe", "券商"),
        ["600958"] = new("600958", "东方证券", "dongfang", "券商"),
        ["601788"] = new("601788", "光大证券", "guangda", "券商"),
        ["601878"] = new("601878", "浙商证券", "zheshang", "券商"),
        ["002736"] = new("002736", "国信证券", "guoxin", "券商"),
        ["600918"] = new("600918", "中泰证券", "zhongtai", "券商"),
        ["000783"] = new("000783", "长江证券", "changjiang", "券商"),
        ["000686"] = new("000686", "东北证券", "dongbei", "券商"),
        ["000728"] = new("000728", "国元证券", "guoyuan", "券商"),
        ["002500"] = new("002500", "山西证券", "shanxi", "券商"),

        // ===== 金融 - 保险 =====
        ["601318"] = new("601318", "中国平安", "pingan", "保险"),
        ["601628"] = new("601628", "中国人寿", "guoren", "保险"),
        ["601601"] = new("601601", "中国太保", "taibao", "保险"),
        ["601336"] = new("601336", "新华保险", "xinhua", "保险"),
        ["601319"] = new("601319", "中国人保", "renbao", "保险"),

        // ===== 消费 - 白酒/食品/家电 =====
        ["600519"] = new("600519", "贵州茅台", "maotai", "白酒"),
        ["000858"] = new("000858", "五粮液", "wuliangye", "白酒"),
        ["000568"] = new("000568", "泸州老窖", "luzhou", "白酒"),
        ["002304"] = new("002304", "洋河股份", "yanghe", "白酒"),
        ["600809"] = new("600809", "山西汾酒", "fenjiu", "白酒"),
        ["000596"] = new("000596", "古井贡酒", "gujing", "白酒"),
        ["603369"] = new("603369", "今世缘", "jinshiyuan", "白酒"),
        ["603589"] = new("603589", "口子窖", "kouzijiao", "白酒"),
        ["600702"] = new("600702", "舍得酒业", "shede", "白酒"),
        ["000799"] = new("000799", "酒鬼酒", "jiugui", "白酒"),
        ["600779"] = new("600779", "水井坊", "shuijingfang", "白酒"),
        ["600559"] = new("600559", "老白干酒", "laobaigan", "白酒"),
        ["000333"] = new("000333", "美的集团", "meidi", "家电"),
        ["000651"] = new("000651", "格力电器", "geli", "家电"),
        ["600690"] = new("600690", "海尔智家", "haier", "家电"),
        ["000921"] = new("000921", "海信家电", "haixin", "家电"),
        ["002032"] = new("002032", "苏泊尔", "supor", "家电"),
        ["002242"] = new("002242", "九阳股份", "jiuyang", "家电"),
        ["002508"] = new("002508", "老板电器", "laoban", "家电"),
        ["002035"] = new("002035", "华帝股份", "huadi", "家电"),
        ["603868"] = new("603868", "飞科电器", "feike", "家电"),
        ["600887"] = new("600887", "伊利股份", "yili", "乳制品"),
        ["603288"] = new("603288", "海天味业", "haitian", "调味品"),
        ["000895"] = new("000895", "双汇发展", "shuanghui", "肉制品"),
        ["002507"] = new("002507", "涪陵榨菜", "fuling", "榨菜"),
        ["603345"] = new("603345", "安井食品", "anjing", "速冻"),
        ["002557"] = new("002557", "洽洽食品", "qiaqia", "坚果"),
        ["300783"] = new("300783", "三只松鼠", "songshu", "零食"),
        ["002847"] = new("002847", "盐津铺子", "yanjin", "零食"),
        ["603719"] = new("603719", "良品铺子", "liangpin", "零食"),
        ["603517"] = new("603517", "绝味食品", "juewei", "卤味"),
        ["603866"] = new("603866", "桃李面包", "taoli", "烘焙"),

        // ===== 医药 =====
        ["600276"] = new("600276", "恒瑞医药", "hengrui", "创新药"),
        ["300760"] = new("300760", "迈瑞医疗", "mairui", "医疗器械"),
        ["603259"] = new("603259", "药明康德", "yaoming", "CRO"),
        ["300015"] = new("300015", "爱尔眼科", "aier", "医疗服务"),
        ["600436"] = new("600436", "片仔癀", "pianzaihuang", "中药"),
        ["000538"] = new("000538", "云南白药", "yunnan", "中药"),
        ["600196"] = new("600196", "复星医药", "fuxing", "医药"),
        ["300122"] = new("300122", "智飞生物", "zhifei", "疫苗"),
        ["000661"] = new("000661", "长春高新", "changchun", "生物医药"),
        ["002007"] = new("002007", "华兰生物", "hualan", "血液制品"),
        ["300142"] = new("300142", "沃森生物", "wosen", "疫苗"),
        ["000513"] = new("000513", "丽珠集团", "lizhu", "医药"),
        ["600380"] = new("600380", "健康元", "jiankangyuan", "医药"),
        ["600079"] = new("600079", "人福医药", "renfu", "医药"),
        ["300003"] = new("300003", "乐普医疗", "lepu", "医疗器械"),
        ["300347"] = new("300347", "泰格医药", "taige", "CRO"),

        // ===== 新能源 =====
        ["300750"] = new("300750", "宁德时代", "ningde", "电池"),
        ["002594"] = new("002594", "比亚迪", "byd", "新能源车"),
        ["300274"] = new("300274", "阳光电源", "yangguang", "光伏逆变器"),
        ["601012"] = new("601012", "隆基绿能", "longji", "光伏"),
        ["600438"] = new("600438", "通威股份", "tongwei", "光伏"),
        ["300014"] = new("300014", "亿纬锂能", "yiwei", "锂电池"),
        ["002460"] = new("002460", "赣锋锂业", "ganfeng", "锂矿"),
        ["002709"] = new("002709", "天赐材料", "tianci", "电解液"),
        ["603806"] = new("603806", "福斯特", "fusite", "光伏胶膜"),
        ["002459"] = new("002459", "晶澳科技", "jingao", "光伏组件"),
        ["688599"] = new("688599", "天合光能", "tianhe", "光伏组件"),
        ["601877"] = new("601877", "正泰电器", "zhengtai", "光伏"),
        ["002074"] = new("002074", "国轩高科", "guoxuan", "电池"),
        ["300037"] = new("300037", "新宙邦", "xinzhoubang", "电解液"),
        ["603799"] = new("603799", "华友钴业", "huayou", "钴"),

        // ===== 半导体/科技 =====
        ["688981"] = new("688981", "中芯国际", "zhongxin", "半导体"),
        ["603501"] = new("603501", "韦尔股份", "weier", "半导体"),
        ["002371"] = new("002371", "北方华创", "beifang", "半导体设备"),
        ["688041"] = new("688041", "海光信息", "haiguang", "服务器芯片"),
        ["688256"] = new("688256", "寒武纪", "hanwuji", "AI 芯片"),
        ["603986"] = new("603986", "兆易创新", "zhaoyi", "存储芯片"),
        ["300782"] = new("300782", "卓胜微", "zhuosheng", "射频芯片"),
        ["300661"] = new("300661", "圣邦股份", "shengbang", "模拟芯片"),
        ["603160"] = new("603160", "汇顶科技", "huiding", "指纹芯片"),
        ["600460"] = new("600460", "士兰微", "shilan", "半导体"),
        ["600584"] = new("600584", "长电科技", "changdian", "封测"),
        ["002156"] = new("002156", "通富微电", "tongfu", "封测"),
        ["002185"] = new("002185", "华天科技", "huatian", "封测"),
        ["002049"] = new("002049", "紫光国微", "ziguang", "芯片设计"),
        ["000725"] = new("000725", "京东方A", "boed", "面板"),
        ["000100"] = new("000100", "TCL科技", "tcl", "面板"),
        ["002475"] = new("002475", "立讯精密", "luxun", "消费电子"),
        ["002241"] = new("002241", "歌尔股份", "geer", "声学"),
        ["300433"] = new("300433", "蓝思科技", "lansi", "玻璃盖板"),
        ["002230"] = new("002230", "科大讯飞", "kedaxunfei", "AI"),
        ["300496"] = new("300496", "中科创达", "zhongkechuangda", "智能汽车"),
        ["300223"] = new("300223", "北京君正", "beijingjunzheng", "芯片"),

        // ===== 互联网（港股） =====
        ["00700"] = new("00700", "腾讯控股", "tencent", "互联网"),
        ["09988"] = new("09988", "阿里巴巴-W", "alibaba", "电商"),
        ["03690"] = new("03690", "美团-W", "meituan", "本地生活"),
        ["01024"] = new("01024", "快手-W", "kuaishou", "短视频"),
        ["09618"] = new("09618", "京东集团-SW", "jd", "电商"),
        ["09888"] = new("09888", "百度集团-SW", "baidu", "搜索"),
        ["09999"] = new("09999", "网易-S", "wangyi", "游戏"),
        ["09961"] = new("09961", "携程集团-S", "xiecheng", "OTA"),
        ["01070"] = new("01070", "TCL电子", "tcl", "电子"),
        ["00241"] = new("00241", "阿里健康", "alijiankang", "医疗"),
        ["06618"] = new("06618", "京东健康", "jdjk", "医疗"),

        // ===== 汽车 =====
        ["002594"] = new("002594", "比亚迪", "byd", "汽车"),
        ["601633"] = new("601633", "长城汽车", "changcheng", "汽车"),
        ["0175"] = new("00175", "吉利汽车", "jili", "汽车"),
        ["601238"] = new("601238", "广汽集团", "guangqi", "汽车"),
        ["600104"] = new("600104", "上汽集团", "shangqi", "汽车"),
        ["000625"] = new("000625", "长安汽车", "changan", "汽车"),
        ["600166"] = new("600166", "福田汽车", "futian", "商用车"),
        ["600066"] = new("600066", "宇通客车", "yutong", "客车"),
        ["000951"] = new("000951", "中国重汽", "zhongqi", "重卡"),
        ["600741"] = new("600741", "华域汽车", "huayu", "汽车零部件"),
        ["600660"] = new("600660", "福耀玻璃", "fuyao", "汽车玻璃"),
        ["601799"] = new("601799", "星宇股份", "xingyu", "车灯"),
        ["601058"] = new("601058", "赛轮轮胎", "sailun", "轮胎"),
        ["601966"] = new("601966", "玲珑轮胎", "linglong", "轮胎"),

        // ===== 房地产 =====
        ["000002"] = new("000002", "万科A", "wanke", "地产"),
        ["600048"] = new("600048", "保利发展", "baoli", "地产"),
        ["001979"] = new("001979", "招商蛇口", "zhaoshangshekou", "地产"),
        ["1109"] = new("01109", "华润置地", "huarun", "地产"),
        ["600383"] = new("600383", "金地集团", "jindi", "地产"),
        ["601155"] = new("601155", "新城控股", "xincheng", "地产"),
        ["600340"] = new("600340", "华夏幸福", "huaxia", "地产"),
        ["001914"] = new("001914", "招商积余", "zhaoshangjiyu", "物业"),

        // ===== 化工/材料 =====
        ["600309"] = new("600309", "万华化学", "wanhua", "化工"),
        ["600346"] = new("600346", "恒力石化", "hengl", "石化"),
        ["002493"] = new("002493", "荣盛石化", "rongsheng", "石化"),
        ["000301"] = new("000301", "东方盛虹", "dongfangshenghong", "石化"),
        ["600426"] = new("600426", "华鲁恒升", "hualuhengsheng", "化工"),
        ["600486"] = new("600486", "扬农化工", "yangnong", "农药"),
        ["002001"] = new("002001", "新和成", "xinhecheng", "维生素"),
        ["600352"] = new("600352", "浙江龙盛", "longsheng", "染料"),

        // ===== 钢铁/煤炭/有色 =====
        ["600019"] = new("600019", "宝钢股份", "baogang", "钢铁"),
        ["000959"] = new("000959", "首钢股份", "shougang", "钢铁"),
        ["600010"] = new("600010", "包钢股份", "baogang", "钢铁"),
        ["000932"] = new("000932", "华菱钢铁", "hualing", "钢铁"),
        ["601899"] = new("601899", "紫金矿业", "zijin", "有色"),
        ["603993"] = new("603993", "洛阳钼业", "luoyangmoye", "有色"),
        ["601600"] = new("601600", "中国铝业", "zhonglv", "铝"),
        ["600362"] = new("600362", "江西铜业", "jiangxitong", "铜"),
        ["600547"] = new("600547", "山东黄金", "shandonghuangjin", "黄金"),
        ["600111"] = new("600111", "北方稀土", "beifangxitu", "稀土"),
        ["601088"] = new("601088", "中国神华", "shenhua", "煤炭"),
        ["601225"] = new("601225", "陕西煤业", "shanximei", "煤炭"),
        ["601898"] = new("601898", "中煤能源", "zhongmei", "煤炭"),
        ["600188"] = new("600188", "兖矿能源", "yankuang", "煤炭"),
        ["000983"] = new("000983", "山西焦煤", "shanxijiaomei", "焦煤"),
        ["601666"] = new("601666", "平煤股份", "pingmei", "煤炭"),
        ["601857"] = new("601857", "中国石油", "zhongshiyou", "石油"),
        ["600028"] = new("600028", "中国石化", "zhongshihua", "石化"),
        ["601808"] = new("601808", "中海油服", "zhonghaiyoufu", "油服"),
        ["600583"] = new("600583", "海油工程", "haiyougongcheng", "海工"),
        ["601233"] = new("601233", "桐昆股份", "tongkun", "涤纶"),
        ["002648"] = new("002648", "卫星化学", "weixing", "烯烃"),

        // ===== 基建/工程 =====
        ["601668"] = new("601668", "中国建筑", "zhongjian", "建筑"),
        ["601390"] = new("601390", "中国中铁", "zhongtie", "基建"),
        ["601186"] = new("601186", "中国铁建", "zhongtiejian", "基建"),
        ["601800"] = new("601800", "中国交建", "zhongjiaojian", "基建"),
        ["601669"] = new("601669", "中国电建", "zhongdianjian", "电建"),
        ["601868"] = new("601868", "中国能建", "zhongnengjian", "能建"),
        ["601117"] = new("601117", "中国化学", "zhonghuaxue", "化工工程"),

        // ===== 通信/计算机 =====
        ["000063"] = new("000063", "中兴通讯", "zhongxing", "通信"),
        ["600498"] = new("600498", "烽火通信", "fenghuo", "通信"),
        ["600050"] = new("600050", "中国联通", "liantong", "运营商"),
        ["601728"] = new("601728", "中国电信", "dianxin", "运营商"),
        ["600941"] = new("600941", "中国移动", "yidong", "运营商"),
        ["600588"] = new("600588", "用友网络", "yongyou", "ERP"),
        ["002230"] = new("002230", "科大讯飞", "kedaxunfei", "AI"),
        ["300033"] = new("300033", "同花顺", "tonghuashun", "金融 IT"),
        ["600570"] = new("600570", "恒生电子", "hengsheng", "金融 IT"),
        ["300454"] = new("300454", "深信服", "shenxinfu", "网络安全"),

        // ===== 农业/养殖 =====
        ["002714"] = new("002714", "牧原股份", "muyuan", "生猪养殖"),
        ["300498"] = new("300498", "温氏股份", "wenshi", "生猪养殖"),
        ["000876"] = new("000876", "新希望", "xinxiwang", "饲料"),
        ["002311"] = new("002311", "海大集团", "haida", "饲料"),
        ["002385"] = new("002385", "大北农", "dabeinong", "饲料"),
        ["000998"] = new("000998", "隆平高科", "longping", "种业"),
        ["002041"] = new("002041", "登海种业", "denghai", "种业"),

        // ===== 传媒/娱乐 =====
        ["002027"] = new("002027", "分众传媒", "fenzhong", "电梯广告"),
        ["300413"] = new("300413", "芒果超媒", "mangguo", "视频"),
        ["300251"] = new("300251", "光线传媒", "guangxian", "电影"),
        ["300133"] = new("300133", "华策影视", "huace", "影视"),
        ["600977"] = new("600977", "中国电影", "zhongguodianying", "电影"),
        ["002739"] = new("002739", "万达电影", "wandadianying", "院线"),

        // ===== 物流/快递 =====
        ["002352"] = new("002352", "顺丰控股", "shunfeng", "快递"),
        ["600233"] = new("600233", "圆通速递", "yuantong", "快递"),
        ["002468"] = new("002468", "申通快递", "shentong", "快递"),
        ["002120"] = new("002120", "韵达股份", "yunda", "快递"),

        // ===== 国防军工 =====
        ["600150"] = new("600150", "中国船舶", "zhongguochuanbo", "船舶"),
        ["600760"] = new("600760", "中航沈飞", "zhonghangshenfei", "战机"),
        ["000768"] = new("000768", "中航西飞", "zhonghangxifei", "运输机"),
        ["600893"] = new("600893", "航发动力", "hangfadongli", "发动机"),
        ["600038"] = new("600038", "中直股份", "zhongzhi", "直升机"),
        ["600316"] = new("600316", "洪都航空", "hongdu", "教练机"),

        // ===== 化妆品/服装 =====
        ["603605"] = new("603605", "珀莱雅", "polaiya", "化妆品"),
        ["600315"] = new("600315", "上海家化", "shanghaijiahua", "日化"),
        ["603983"] = new("603983", "丸美股份", "wanmei", "化妆品"),
        ["600398"] = new("600398", "海澜之家", "hailan", "男装"),
        ["600177"] = new("600177", "雅戈尔", "yageer", "服装"),
        ["002563"] = new("002563", "森马服饰", "senma", "服装"),
        ["603877"] = new("603877", "太平鸟", "taipingniao", "服装"),
        ["603587"] = new("603587", "地素时尚", "disu", "女装"),

        // ===== 通信运营商/公用事业 =====
        ["600900"] = new("600900", "长江电力", "changjiang", "电力"),
        ["601985"] = new("601985", "中国核电", "zhonghedian", "核电"),
        ["600886"] = new("600886", "国投电力", "guotou", "电力"),
        ["600011"] = new("600011", "华能国际", "huaneng", "电力"),
        ["600795"] = new("600795", "国电电力", "guodian", "电力"),

        // ===== 其他/小盘 =====
        ["603259"] = new("603259", "药明康德", "yaoming", "CRO"),
        ["000538"] = new("000538", "云南白药", "yunnan", "中药"),
        ["600196"] = new("600196", "复星医药", "fuxing", "医药"),
    };

    public List<CompanyInfo> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<CompanyInfo>();

        keyword = keyword.Trim().ToLowerInvariant();
        var results = new List<(CompanyInfo company, int score)>();

        foreach (var (code, company) in _companies)
        {
            int score = MatchScore(keyword, code, company);
            if (score > 0)
                results.Add((company, score));
        }

        return results
            .OrderByDescending(x => x.score)
            .Take(10)
            .Select(x => x.company)
            .ToList();
    }

    public async Task<CompanyInfo?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        // 1. 本地库查
        var local = _companies.GetValueOrDefault(code);
        if (local != null) return local;

        // 2. 外部回退（东方财富）
        if (_eastMoney == null) return null;
        _logger?.LogInformation("本地库未命中 {Code}，查东方财富", code);
        var external = await _eastMoney.LookupByCodeAsync(code, ct);
        return external;
    }

    public CompanyInfo? FindByName(string name)
    {
        var results = Search(name);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// 评分：数字代码精确 = 100，代码包含 = 80，
    /// 名称精确 = 90，名称包含 = 70，拼音 = 60，行业 = 50
    /// </summary>
    private static int MatchScore(string keyword, string code, CompanyInfo company)
    {
        if (code == keyword) return 100;
        if (code.StartsWith(keyword)) return 80;
        if (string.Equals(company.Name, keyword, StringComparison.OrdinalIgnoreCase)) return 90;
        if (company.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 70;
        if (company.Pinyin.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 60;
        if (company.Industry.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 50;
        return 0;
    }
}

public record CompanyInfo(string Code, string Name, string Pinyin, string Industry);

public interface ICompanySearchService
{
    List<CompanyInfo> Search(string keyword);
    Task<CompanyInfo?> FindByCodeAsync(string code, CancellationToken ct = default);
    CompanyInfo? FindByName(string name);
}
