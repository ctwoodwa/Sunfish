import type { Meta, StoryObj } from '@storybook/react';
import { SunfishButton } from './SunfishButton';
import { ButtonVariant } from '../../contracts/ButtonVariant';
import { ButtonSize } from '../../contracts/ButtonSize';
import { FillMode } from '../../contracts/FillMode';
import { RoundedMode } from '../../contracts/RoundedMode';

const meta: Meta<typeof SunfishButton> = {
  title: 'Components/SunfishButton',
  component: SunfishButton,
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: Object.values(ButtonVariant),
    },
    size: { control: { type: 'select' }, options: Object.values(ButtonSize) },
    fillMode: { control: { type: 'select' }, options: Object.values(FillMode) },
    rounded: { control: { type: 'select' }, options: Object.values(RoundedMode) },
  },
  args: {
    children: 'Sunfish Button',
    variant: ButtonVariant.Primary,
    size: ButtonSize.Medium,
    fillMode: FillMode.Filled,
    rounded: RoundedMode.Medium,
    disabled: false,
  },
};

export default meta;
type Story = StoryObj<typeof SunfishButton>;

export const Bootstrap: Story = {
  parameters: { sunfishProvider: 'bootstrap' },
};

export const Fluent: Story = {
  parameters: { sunfishProvider: 'fluent' },
};

export const Material: Story = {
  parameters: { sunfishProvider: 'material' },
};
