// Ranks tab: the runner's own dashboard (FR-22: land, territories, distance,
// runs, time, ranks) and the Karachi leaderboard by area, distance or
// territory count (FR-23). Private runners show as "Anonymous profile".

import { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { authFetch, errorMessage } from '../services/api';
import {
  Leaderboard,
  LeaderboardEntry,
  MyStats,
  RankMetric,
  formatActiveTime,
  formatRank,
  formatRankValue,
} from '../services/ranks';

const METRICS: { key: RankMetric; label: string }[] = [
  { key: 'area', label: 'AREA' },
  { key: 'distance', label: 'DISTANCE' },
  { key: 'territories', label: 'TERRITORIES' },
];

async function getJson<T>(path: string): Promise<T> {
  const response = await authFetch(path);
  if (!response.ok) {
    throw new Error(
      response.status === 401
        ? 'Your session has expired. Please log in again.'
        : 'Unable to load rankings.'
    );
  }
  return response.json();
}

export default function RanksScreen() {
  const [metric, setMetric] = useState<RankMetric>('area');
  const [stats, setStats] = useState<MyStats | null>(null);
  const [board, setBoard] = useState<Leaderboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async (chosen: RankMetric) => {
    setError('');
    try {
      const [myStats, leaderboard] = await Promise.all([
        getJson<MyStats>('/api/stats/me'),
        getJson<Leaderboard>(`/api/leaderboard?metric=${chosen}`),
      ]);
      setStats(myStats);
      setBoard(leaderboard);
    } catch (err) {
      setError(errorMessage(err, 'Something went wrong.'));
    }
  }, []);

  // Load when the tab opens and whenever the ranking type changes.
  useEffect(() => {
    let active = true;
    setLoading(true);
    load(metric).finally(() => {
      if (active) setLoading(false);
    });
    return () => {
      active = false;
    };
  }, [metric, load]);

  const onRefresh = async () => {
    setRefreshing(true);
    await load(metric);
    setRefreshing(false);
  };

  if (loading && !board) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#C6FF35" />
      </View>
    );
  }

  if (error && !board) {
    return (
      <View style={styles.center}>
        <Ionicons name="alert-circle-outline" size={42} color="#FF7777" />
        <Text style={styles.errorText}>{error}</Text>
        <TouchableOpacity onPress={() => load(metric)} style={styles.retryButton}>
          <Text style={styles.retryText}>TRY AGAIN</Text>
        </TouchableOpacity>
      </View>
    );
  }

  const meOutsideTop =
    board?.me && !board.entries.some((entry) => entry.isMine) ? board.me : null;

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#C6FF35" />
        }
      >
        <Text style={styles.heading}>RANKS</Text>

        {stats && (
          <>
            <Text style={styles.sectionTitle}>YOUR STATS</Text>
            <View style={styles.infoCard}>
              <View style={styles.statGrid}>
                <Stat label="LAND" value={formatRankValue('area', stats.totalAreaSquareMeters)} />
                <Stat label="TERRITORIES" value={String(stats.territoryCount)} />
                <Stat label="DISTANCE" value={formatRankValue('distance', stats.totalDistanceMeters)} />
                <Stat label="RUNS" value={String(stats.completedRuns)} />
                <Stat label="TIME" value={formatActiveTime(stats.totalActiveSeconds)} />
                <Stat
                  label="SHIELDED / OPEN"
                  value={`${stats.shieldedCount} / ${stats.vulnerableCount}`}
                />
              </View>
              <View style={styles.divider} />
              <View style={styles.rankRow}>
                <Stat label="AREA RANK" value={formatRank(stats.areaRank)} />
                <Stat label="DISTANCE RANK" value={formatRank(stats.distanceRank)} />
                <Stat label="LAND RANK" value={formatRank(stats.territoryRank)} />
              </View>
            </View>
          </>
        )}

        <Text style={styles.sectionTitle}>KARACHI</Text>
        <View style={styles.metricRow} accessibilityRole="tablist">
          {METRICS.map((item) => {
            const active = item.key === metric;
            return (
              <TouchableOpacity
                key={item.key}
                style={[styles.metricButton, active && styles.metricButtonActive]}
                onPress={() => setMetric(item.key)}
                accessibilityRole="tab"
                accessibilityState={{ selected: active }}
              >
                <Text style={[styles.metricText, active && styles.metricTextActive]}>
                  {item.label}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>

        {error ? <Text style={styles.inlineError}>{error}</Text> : null}

        {board && board.entries.length === 0 ? (
          <View style={styles.infoCard}>
            <Text style={styles.emptyText}>
              No runner is ranked yet. Close a loop around a block to be the first.
            </Text>
          </View>
        ) : (
          <View style={styles.listCard}>
            {board?.entries.map((entry) => (
              <RankRow key={`${entry.rank}-${entry.name}-${entry.colorHex}`} entry={entry} metric={metric} />
            ))}
          </View>
        )}

        {meOutsideTop && (
          <>
            <Text style={styles.sectionTitle}>YOUR POSITION</Text>
            <View style={styles.listCard}>
              <RankRow entry={meOutsideTop} metric={metric} />
            </View>
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

function RankRow({ entry, metric }: { entry: LeaderboardEntry; metric: RankMetric }) {
  return (
    <View
      style={[styles.row, entry.isMine && styles.rowMine]}
      accessible
      accessibilityLabel={`Rank ${entry.rank}, ${entry.name}, ${formatRankValue(metric, entry.value)}`}
    >
      <Text style={[styles.rank, entry.rank <= 3 && styles.rankTop]}>{entry.rank}</Text>
      <View style={[styles.colorDot, { backgroundColor: entry.colorHex }]} />
      <Text style={styles.name} numberOfLines={1}>
        {entry.name}
        {entry.isMine ? '  (you)' : ''}
      </Text>
      <Text style={styles.rowValue}>{formatRankValue(metric, entry.value)}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  content: {
    padding: 24,
    paddingBottom: 40,
  },
  center: {
    flex: 1,
    backgroundColor: '#0C0D0C',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
  },
  heading: {
    color: '#C6FF35',
    fontSize: 26,
    fontWeight: '900',
    letterSpacing: 2,
    marginBottom: 5,
  },
  sectionTitle: {
    color: '#999E99',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1.5,
    marginTop: 30,
    marginBottom: 12,
  },
  infoCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 12,
    padding: 20,
  },
  statGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    rowGap: 18,
  },
  rankRow: {
    flexDirection: 'row',
  },
  stat: {
    width: '33.33%',
    alignItems: 'center',
  },
  statValue: {
    color: '#FFFFFF',
    fontSize: 17,
    fontWeight: '900',
    marginBottom: 4,
  },
  label: {
    color: '#777D77',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.5,
    textAlign: 'center',
  },
  divider: {
    height: 1,
    backgroundColor: '#303330',
    marginVertical: 18,
  },
  metricRow: {
    flexDirection: 'row',
    backgroundColor: '#1C1E1C',
    borderRadius: 12,
    padding: 4,
    marginBottom: 12,
  },
  metricButton: {
    flex: 1,
    minHeight: 44,
    borderRadius: 9,
    alignItems: 'center',
    justifyContent: 'center',
  },
  metricButtonActive: {
    backgroundColor: '#C6FF35',
  },
  metricText: {
    color: '#999E99',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1,
  },
  metricTextActive: {
    color: '#0C0D0C',
  },
  listCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 12,
    paddingVertical: 4,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderWidth: 1,
    borderColor: 'transparent',
    borderRadius: 12,
  },
  rowMine: {
    borderColor: '#C6FF35',
  },
  rank: {
    width: 32,
    color: '#999E99',
    fontSize: 15,
    fontWeight: '900',
  },
  rankTop: {
    color: '#C6FF35',
  },
  colorDot: {
    width: 12,
    height: 12,
    borderRadius: 6,
    marginRight: 12,
  },
  name: {
    flex: 1,
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  rowValue: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '800',
    marginLeft: 10,
  },
  emptyText: {
    color: '#999E99',
    fontSize: 14,
    textAlign: 'center',
  },
  inlineError: {
    color: '#FF7777',
    fontSize: 12,
    marginBottom: 10,
  },
  retryButton: {
    marginTop: 20,
    padding: 15,
  },
  retryText: {
    color: '#C6FF35',
    fontWeight: '800',
  },
  errorText: {
    color: '#FF7777',
    textAlign: 'center',
    marginTop: 12,
  },
});
