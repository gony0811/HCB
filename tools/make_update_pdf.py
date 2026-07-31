# -*- coding: utf-8 -*-
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, Table,
                                TableStyle, HRFlowable)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont

# 한글 CID 폰트 등록
pdfmetrics.registerFont(UnicodeCIDFont('HYSMyeongJo-Medium'))
pdfmetrics.registerFont(UnicodeCIDFont('HYGothic-Medium'))
KFONT = 'HYGothic-Medium'
KFONT_M = 'HYSMyeongJo-Medium'

# 색상
NAVY = colors.HexColor('#1F3A5F')
ACCENT = colors.HexColor('#2E6CA4')
LIGHT = colors.HexColor('#EAF1F8')
GREY = colors.HexColor('#666666')
LINE = colors.HexColor('#CCD6E0')

styles = getSampleStyleSheet()

title_style = ParagraphStyle('T', fontName=KFONT, fontSize=22, textColor=NAVY,
                             leading=28, spaceAfter=2)
sub_style = ParagraphStyle('S', fontName=KFONT_M, fontSize=10.5, textColor=GREY,
                           leading=15)
date_style = ParagraphStyle('D', fontName=KFONT, fontSize=12.5, textColor=colors.white,
                            leading=16)
cat_style = ParagraphStyle('C', fontName=KFONT, fontSize=9, textColor=ACCENT,
                           leading=13)
item_style = ParagraphStyle('I', fontName=KFONT_M, fontSize=9.5, textColor=colors.HexColor('#222222'),
                            leading=14)
foot_style = ParagraphStyle('F', fontName=KFONT_M, fontSize=8, textColor=GREY, leading=11)

# ── 업데이트 내역 데이터 (최신순) ─────────────────────────────
# (날짜, [ (분류, [항목들]) ... ])
updates = [
    ("2026-07-31", [
        ("Btm 정렬", [
            "BTM θ 보정 시퀀스 추가 (RECIPE=DIE 전용, BtmHighAlign) — 수행 순서: 카메라 거리 측정 → BTM Die 측정 → θ 보정 → BTM Die 재측정",
            "카메라 거리(Hc2Offset) 기반으로 BLA(HC1)·BRA(HC2) 얼라인마크를 통합해 상대 각도 계산",
            "BLA→BRA 상대거리(X,Y)를 레시피에 저장, 계산한 θ만큼 W_T 회전 보정",
            "회전 부호·데드밴드를 EC 파라미터(BtmThetaSign / BtmThetaMinDeg)로 조정 가능",
        ]),
    ]),
    ("2026-07-30", [
        ("Wafer 정렬", [
            "Wafer Theta 보정 시퀀스 개선 — Die마다 점진적으로 각도 보정, 끝 Die 도달 후 역주행 로직 추가",
            "Wafer 시퀀스 세부 수정사항 반영",
        ]),
    ]),
]

def build():
    doc = SimpleDocTemplate("HCB_업데이트_내역.pdf", pagesize=A4,
                            leftMargin=18*mm, rightMargin=18*mm,
                            topMargin=16*mm, bottomMargin=15*mm,
                            title="HCB 업데이트 내역")
    story = []
    story.append(Paragraph("HCB 개발 업데이트 내역", title_style))
    story.append(Paragraph("Hybrid Chip Bonding Machine — 작업 내역 리스트", sub_style))
    story.append(Spacer(1, 4))
    story.append(HRFlowable(width="100%", thickness=1.4, color=NAVY, spaceAfter=10))

    for date, cats in updates:
        # 날짜 헤더 밴드
        dband = Table([[Paragraph(date, date_style)]], colWidths=[174*mm])
        dband.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), NAVY),
            ('LEFTPADDING', (0,0), (-1,-1), 8),
            ('TOPPADDING', (0,0), (-1,-1), 4),
            ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ]))
        story.append(dband)
        story.append(Spacer(1, 4))

        rows = []
        for cat, items in cats:
            item_para = [Paragraph("• " + it, item_style) for it in items]
            # 항목들을 하나의 셀에 넣기 위해 조합
            inner = Table([[p] for p in item_para], colWidths=[142*mm])
            inner.setStyle(TableStyle([
                ('LEFTPADDING', (0,0), (-1,-1), 0),
                ('RIGHTPADDING', (0,0), (-1,-1), 0),
                ('TOPPADDING', (0,0), (-1,-1), 1.5),
                ('BOTTOMPADDING', (0,0), (-1,-1), 1.5),
            ]))
            rows.append([Paragraph(cat, cat_style), inner])

        t = Table(rows, colWidths=[30*mm, 144*mm])
        t.setStyle(TableStyle([
            ('VALIGN', (0,0), (-1,-1), 'TOP'),
            ('BACKGROUND', (0,0), (0,-1), LIGHT),
            ('LINEBELOW', (0,0), (-1,-2), 0.5, LINE),
            ('LEFTPADDING', (0,0), (0,-1), 6),
            ('RIGHTPADDING', (1,0), (1,-1), 4),
            ('TOPPADDING', (0,0), (-1,-1), 6),
            ('BOTTOMPADDING', (0,0), (-1,-1), 6),
            ('LINEBEFORE', (0,0), (0,-1), 2, ACCENT),
        ]))
        story.append(t)
        story.append(Spacer(1, 12))

    story.append(HRFlowable(width="100%", thickness=0.6, color=LINE, spaceBefore=4, spaceAfter=6))
    story.append(Paragraph(
        "본 문서는 Git 커밋 이력을 기반으로 자동 정리되었습니다. · 생성일: 2026-07-31",
        foot_style))

    doc.build(story)
    print("PDF 생성 완료")

build()
