/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */
package com.sitecVendor.gridGhostImporter;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Minimal dependency-free JSON parser and builder. */
public final class NxJson {

  private NxJson() {}

  public static String buildObject(Map<String, Object> obj) {
    StringBuilder sb = new StringBuilder("{");
    boolean first = true;
    for (Map.Entry<String, Object> entry : obj.entrySet()) {
      if (!first) sb.append(',');
      first = false;
      sb.append('"').append(escape(entry.getKey())).append("\":");
      appendValue(sb, entry.getValue());
    }
    return sb.append('}').toString();
  }

  public static Map<String, Object> obj(Object... kv) {
    if (kv.length % 2 != 0) throw new IllegalArgumentException("obj() requires even argument count");
    Map<String, Object> map = new LinkedHashMap<String, Object>();
    for (int i = 0; i < kv.length; i += 2) map.put((String) kv[i], kv[i + 1]);
    return map;
  }

  public static List<Object> arr(Object... items) {
    return new ArrayList<Object>(Arrays.asList(items));
  }

  @SuppressWarnings("unchecked")
  private static void appendValue(StringBuilder sb, Object value) {
    if (value == null) {
      sb.append("null");
    } else if (value instanceof Boolean || value instanceof Number) {
      sb.append(value);
    } else if (value instanceof String) {
      sb.append('"').append(escape((String) value)).append('"');
    } else if (value instanceof Map) {
      sb.append(buildObject((Map<String, Object>) value));
    } else if (value instanceof List) {
      sb.append(buildArray((List<Object>) value));
    } else {
      sb.append('"').append(escape(value.toString())).append('"');
    }
  }

  private static String buildArray(List<Object> arr) {
    StringBuilder sb = new StringBuilder("[");
    boolean first = true;
    for (Object v : arr) {
      if (!first) sb.append(',');
      first = false;
      appendValue(sb, v);
    }
    return sb.append(']').toString();
  }

  private static String escape(String s) {
    if (s == null) return "";
    StringBuilder sb = new StringBuilder(s.length() + 8);
    for (int i = 0; i < s.length(); i++) {
      char c = s.charAt(i);
      switch (c) {
        case '"':  sb.append("\\\""); break;
        case '\\': sb.append("\\\\"); break;
        case '\n': sb.append("\\n"); break;
        case '\r': sb.append("\\r"); break;
        case '\t': sb.append("\\t"); break;
        case '\b': sb.append("\\b"); break;
        case '\f': sb.append("\\f"); break;
        default:
          if (c < 0x20) sb.append(String.format("\\u%04x", Integer.valueOf((int) c)));
          else sb.append(c);
      }
    }
    return sb.toString();
  }

  public static Map<String, Object> parseObject(String json) {
    if (json == null || json.trim().length() == 0) return new LinkedHashMap<String, Object>();
    Object result = new Parser(json.trim()).parseValue();
    if (!(result instanceof Map)) throw new IllegalArgumentException("Expected JSON object");
    @SuppressWarnings("unchecked")
    Map<String, Object> map = (Map<String, Object>) result;
    return map;
  }

  private static final class Parser {
    private final String src;
    private int pos;

    Parser(String src) { this.src = src; }

    Object parseValue() {
      skipWs();
      if (pos >= src.length()) return null;
      char c = src.charAt(pos);
      if (c == '{') return parseObj();
      if (c == '[') return parseArr();
      if (c == '"') return parseStr();
      if (c == 't') return literal("true", Boolean.TRUE);
      if (c == 'f') return literal("false", Boolean.FALSE);
      if (c == 'n') return literal("null", null);
      if (c == '-' || Character.isDigit(c)) return parseNum();
      throw new IllegalArgumentException("Unexpected char '" + c + "' at " + pos);
    }

    private Map<String, Object> parseObj() {
      Map<String, Object> map = new LinkedHashMap<String, Object>();
      expect('{');
      skipWs();
      if (peek() == '}') { pos++; return map; }
      while (true) {
        skipWs();
        String key = parseStr();
        skipWs();
        expect(':');
        map.put(key, parseValue());
        skipWs();
        char sep = peek();
        if (sep == '}') { pos++; break; }
        if (sep == ',') { pos++; continue; }
        throw new IllegalArgumentException("Expected ',' or '}' at " + pos);
      }
      return map;
    }

    private List<Object> parseArr() {
      List<Object> list = new ArrayList<Object>();
      expect('[');
      skipWs();
      if (peek() == ']') { pos++; return list; }
      while (true) {
        list.add(parseValue());
        skipWs();
        char sep = peek();
        if (sep == ']') { pos++; break; }
        if (sep == ',') { pos++; continue; }
        throw new IllegalArgumentException("Expected ',' or ']' at " + pos);
      }
      return list;
    }

    private String parseStr() {
      expect('"');
      StringBuilder sb = new StringBuilder();
      while (pos < src.length()) {
        char c = src.charAt(pos++);
        if (c == '"') return sb.toString();
        if (c == '\\') {
          if (pos >= src.length()) break;
          char esc = src.charAt(pos++);
          switch (esc) {
            case '"':  sb.append('"'); break;
            case '\\': sb.append('\\'); break;
            case '/':  sb.append('/'); break;
            case 'n':  sb.append('\n'); break;
            case 'r':  sb.append('\r'); break;
            case 't':  sb.append('\t'); break;
            case 'b':  sb.append('\b'); break;
            case 'f':  sb.append('\f'); break;
            case 'u':
              if (pos + 4 <= src.length()) {
                sb.append((char) Integer.parseInt(src.substring(pos, pos + 4), 16));
                pos += 4;
              }
              break;
            default: sb.append(esc);
          }
        } else {
          sb.append(c);
        }
      }
      throw new IllegalArgumentException("Unterminated string at " + pos);
    }

    private Number parseNum() {
      int start = pos;
      if (peek() == '-') pos++;
      while (pos < src.length() && Character.isDigit(src.charAt(pos))) pos++;
      boolean dbl = false;
      if (pos < src.length() && src.charAt(pos) == '.') {
        dbl = true; pos++;
        while (pos < src.length() && Character.isDigit(src.charAt(pos))) pos++;
      }
      if (pos < src.length() && (src.charAt(pos) == 'e' || src.charAt(pos) == 'E')) {
        dbl = true; pos++;
        if (pos < src.length() && (src.charAt(pos) == '+' || src.charAt(pos) == '-')) pos++;
        while (pos < src.length() && Character.isDigit(src.charAt(pos))) pos++;
      }
      String num = src.substring(start, pos);
      if (dbl) return Double.valueOf(Double.parseDouble(num));
      long lv = Long.parseLong(num);
      if (lv >= Integer.MIN_VALUE && lv <= Integer.MAX_VALUE) return Integer.valueOf((int) lv);
      return Long.valueOf(lv);
    }

    private Object literal(String expected, Object value) {
      if (src.startsWith(expected, pos)) { pos += expected.length(); return value; }
      throw new IllegalArgumentException("Expected '" + expected + "' at " + pos);
    }

    private void expect(char c) {
      if (pos >= src.length() || src.charAt(pos) != c)
        throw new IllegalArgumentException("Expected '" + c + "' at " + pos);
      pos++;
    }

    private char peek() { return pos < src.length() ? src.charAt(pos) : '\0'; }

    private void skipWs() {
      while (pos < src.length() && Character.isWhitespace(src.charAt(pos))) pos++;
    }
  }
}
