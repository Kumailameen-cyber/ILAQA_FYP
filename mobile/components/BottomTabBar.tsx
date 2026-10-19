
import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

export type MainTab = 'home' | 'ranks' | 'races' | 'profile';

type BottomTabBarProps = {
  activeTab: MainTab;
  onTabChange: (tab: MainTab) => void;
};

const tabs: {
  key: MainTab;
  label: string;
  icon: React.ComponentProps<typeof Ionicons>['name'];
  activeIcon: React.ComponentProps<typeof Ionicons>['name'];
}[] = [
  {
    key: 'home',
    label: 'HOME',
    icon: 'map-outline',
    activeIcon: 'map',
  },
  {
    key: 'ranks',
    label: 'RANKS',
    icon: 'stats-chart-outline',
    activeIcon: 'stats-chart',
  },
 {
  key: 'races',
  label: 'RACES',
  icon: 'flag-outline',
  activeIcon: 'flag',
},
  {
    key: 'profile',
    label: 'PROFILE',
    icon: 'person-outline',
    activeIcon: 'person',
  },
];

export default function BottomTabBar({
  activeTab,
  onTabChange,
}: BottomTabBarProps) {
  return (
    <View style={styles.container}>
      {tabs.map((tab) => {
        const active = activeTab === tab.key;

        return (
          <TouchableOpacity
            key={tab.key}
            style={styles.tab}
            onPress={() => onTabChange(tab.key)}
            activeOpacity={0.7}
            accessibilityRole="tab"
            accessibilityState={{ selected: active }}
            accessibilityLabel={tab.label}
          >
            <Ionicons
              name={active ? tab.activeIcon : tab.icon}
              size={24}
              color={active ? '#C6FF35' : '#777D77'}
            />

            <Text
              style={[
                styles.label,
                active && styles.activeLabel,
              ]}
            >
              {tab.label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    backgroundColor: '#121512',
    borderTopWidth: 1,
    borderTopColor: '#292D29',
    paddingTop: 13,
    paddingBottom: 10,
    paddingHorizontal: 12,
  },
  tab: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    minHeight: 48,
  },
  label: {
    color: '#777D77',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.6,
  },
  activeLabel: {
    color: '#C6FF35',
  },
});
