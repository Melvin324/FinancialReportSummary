# 合规问答 RAG 系统 — 技术选型

> 项目周期：2026 Q4（10-12 月）
> 简历价值：⭐⭐⭐⭐⭐
> 目标：金融合规领域的 RAG 问答系统，作为转型金融 AI 的核心项目

---

## 1. 业务场景

**用户**：金融行业从业者（合规、风控、运营）
**输入**：自然语言问题（如"私募基金销售可以承诺保本吗？"）
**输出**：基于法规条文的精确回答 + 引用来源

**核心难点**：
- 法规文件长（单部法规可达 10w+ 字）
- 需要精准引用条款
- 幻觉控制要求高（合规不能编）

---

## 2. 技术选型

### 2.1 整体架构

```
用户问题
   ↓
[Query 理解层] ← 改写、扩写、领域识别
   ↓
[检索层] ← 向量召回 + 关键词召回（双路）
   ↓
[Rerank 层] ← 精排，取 Top-5
   ↓
[生成层] ← LLM + 严格 prompt 控制
   ↓
回答 + 引用
```

### 2.2 关键组件选型

| 组件 | 选型 | 理由 |
|------|------|------|
| **LLM** | Claude 3.5 Sonnet | 长上下文、推理能力强、合规场景首选 |
| **Embedding** | bge-large-zh-v1.5 | 中文最强开源 embedding |
| **向量库** | Qdrant | 轻量、部署简单、支持 hybrid search |
| **关系库** | PostgreSQL | 存元数据、用户、日志 |
| **Rerank** | bge-reranker-large | 精排效果好 |
| **框架** | LangChain / LlamaIndex | 快速搭建 |
| **后端** | .NET 8 / Python FastAPI | 看你熟哪个 |
| **前端** | 简单 Web UI（参考 MVP 风格）| |

### 2.3 数据源

- 证监会法规库（公开）
- 银保监会规章
- 基金业协会自律规则
- 内部合规手册（如果有）

> **关键**：合规数据需要持续更新，建议建立自动同步 pipeline

---

## 3. 关键技术决策

### 3.1 Chunk 策略

```python
# 推荐配置
chunk_size = 500          # tokens
chunk_overlap = 50        # tokens
splitter = RecursiveTextSplitter
metadata = {
  "doc_id": "...",        # 原始文档 ID
  "doc_title": "...",
  "article_no": "第X条",  # 条款号
  "effective_date": "...",
  "source_url": "...",
  "category": "..."       # 私募/公募/银行
}
```

**为什么 500 tokens？**
- 法规条款通常较短（< 200 字）
- 500 tokens 大约能装 2-3 个相关条款
- 太小会切断逻辑，太大会降低召回精度

### 3.2 检索策略

**双路召回**：

```python
# 向量召回（语义）
vector_results = qdrant.search(
    query_vector=embed(query),
    limit=20
)

# 关键词召回（BM25）
keyword_results = elasticsearch.search(
    query=query,
    size=20
)

# 合并 + 去重
combined = merge_and_dedup(vector_results, keyword_results)
```

**为什么不只做向量？**
- 法规里有大量专有名词（"私募基金管理人"），关键词匹配更精准
- 双路互补：向量找语义相近，关键词找字面相符

### 3.3 Rerank 必做

召回 Top-20 太粗，必须用 Rerank 模型精排到 Top-5。

```python
from sentence_transformers import CrossEncoder

reranker = CrossEncoder('BAAI/bge-reranker-large')
scores = reranker.predict([
    (query, doc.text) for doc in candidates
])
top5 = sorted(zip(candidates, scores), key=lambda x: -x[1])[:5]
```

### 3.4 Prompt 控制（防幻觉）

```python
SYSTEM_PROMPT = """
你是金融合规专家。请严格根据以下法规条款回答用户问题。

规则：
1. 只使用下方提供的条款内容回答
2. 不得编造条款编号、不得推断未提及的内容
3. 必须标注引用来源（条款号 + 文档名）
4. 如果条款不足以回答，请明确告知"未找到相关条款"
5. 回答结尾附"免责声明"

提供的法规条款：
{context}

用户问题：{question}
"""
```

---

## 4. 评估指标

| 指标 | 目标 | 说明 |
|------|------|------|
| **召回率** | > 90% | 测试集中相关条款被检索到的比例 |
| **引用准确率** | > 95% | 生成的引用是否真实存在于原文 |
| **幻觉率** | < 5% | 答案中不存在的条款或事实 |
| **响应时延** | < 3s | 从提问到拿到答案 |
| **人工评分** | > 4/5 | 合规专家主观评分 |

### 4.1 测试集构建

- 50-100 道真实合规问题（找合规岗同事要）
- 每道题标注"标准答案 + 引用条款"
- 这部分需要花 1-2 周准备

---

## 5. 实施时间线

| 周次 | 任务 | 产出 |
|------|------|------|
| W1 | 数据采集 + 清洗 | 法规语料库 1000+ 条 |
| W2 | Embedding + 向量库搭建 | 检索服务 |
| W3 | Rerank + Prompt 工程 | 端到端 demo |
| W4 | 评估 + 调优 | 评估报告 |
| W5-6 | 前端 + 部署 | 可演示系统 |
| W7-8 | 测试集 + 文档 | 上 GitHub + 简历素材 |

---

## 6. 风险点

| 风险 | 应对 |
|------|------|
| 法规更新滞后 | 每月同步一次，建监控 |
| LLM 幻觉 | 严格 prompt + 引用校验 + 人工抽检 |
| 召回不精准 | 迭代 chunk 策略、引入领域词典 |
| 测试集难拿 | 公开法规问答 + 找律师朋友标注 |

---

## 7. 简历表达模板

> **项目名称**：基于 RAG 的金融合规智能问答系统
> **技术栈**：.NET 8 / Python、Qdrant、Claude 3.5、bge-large-zh、LangChain
> **职责**：架构设计、检索优化、Prompt 工程、评估体系
> **成果**：召回率 90%+，引用准确率 95%+，响应 < 3s

---

## 8. 下一步行动

- [ ] W1 启动前：找 2-3 个合规法规文本源（公开）
- [ ] 决策：后端用 .NET 8 还是 Python FastAPI？（建议 Python，生态更全）
- [ ] 准备 100 道测试题（找合规朋友 or 公开数据集）

---

*文档完成日期：2026-08-29*
